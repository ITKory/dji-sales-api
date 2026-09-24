import { useState, useMemo } from 'react';
import { AreaChart, Area, XAxis, ResponsiveContainer, Tooltip } from 'recharts';
import {
  Users,
  DollarSign,
  TrendingUp,
  Receipt,
  Target,
  Percent,
  ChevronDown,
} from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuCheckboxItem,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';
import { formatCurrency, formatPlainPercent, formatPercent } from '@/lib/format';
import {
  type ManagerRanking,
} from '@/lib/analytics-api';
import { useCountUp } from '@/hooks/use-count-up';

const MANAGER_COLORS = [
  { ring: 'ring-chart-1', bg: 'bg-chart-1/10', text: 'text-chart-1', stroke: 'hsl(var(--chart-1))', bar: 'bg-chart-1' },
  { ring: 'ring-chart-2', bg: 'bg-chart-2/10', text: 'text-chart-2', stroke: 'hsl(var(--chart-2))', bar: 'bg-chart-2' },
  { ring: 'ring-chart-3', bg: 'bg-chart-3/10', text: 'text-chart-3', stroke: 'hsl(var(--chart-3))', bar: 'bg-chart-3' },
];

interface ManagerComparisonProps {
  managers: ManagerRanking[];
  loading?: boolean;
}

function MiniAreaChart({
  data,
  color,
  dataKey,
  formatFn,
}: {
  data: { date: string; revenue: number; grossProfit: number; deals: number }[];
  color: string;
  dataKey: 'revenue' | 'grossProfit' | 'deals';
  formatFn: (v: number) => string;
}) {
  const tooltipFormatter = (v: number) => formatFn(v);
  return (
    <ResponsiveContainer width="100%" height={56}>
      <AreaChart data={data} margin={{ top: 2, right: 0, left: 0, bottom: 0 }}>
        <defs>
          <linearGradient id={`mini-grad-${dataKey}-${color.replace(/[^a-z0-9]/gi, '')}`} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={color} stopOpacity={0.3} />
            <stop offset="100%" stopColor={color} stopOpacity={0} />
          </linearGradient>
        </defs>
        <XAxis dataKey="date" hide />
        <Tooltip
          cursor={false}
          contentStyle={{
            background: 'hsl(var(--popover))',
            border: '1px solid hsl(var(--border))',
            borderRadius: '0.5rem',
            fontSize: '11px',
            padding: '4px 8px',
          }}
          labelStyle={{ color: 'hsl(var(--muted-foreground))', fontSize: '10px' }}
          formatter={tooltipFormatter}
        />
        <Area
          type="monotone"
          dataKey={dataKey}
          stroke={color}
          strokeWidth={2}
          fill={`url(#mini-grad-${dataKey}-${color.replace(/[^a-z0-9]/gi, '')})`}
          dot={false}
          animationDuration={400}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}

function MetricRow({
  icon,
  label,
  values,
  formatFn,
  maxVal,
}: {
  icon: React.ReactNode;
  label: string;
  values: (number | null)[];
  formatFn: (v: number) => string;
  maxVal: number;
}) {
  return (
    <div className="grid grid-cols-[140px_1fr_1fr_1fr] items-center gap-3 border-b border-border/50 py-3 last:border-0">
      <div className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
        {icon}
        {label}
      </div>
      {values.map((val, i) => {
        const pct = maxVal > 0 ? Math.max(0, Math.min(100, ((val ?? 0) / maxVal) * 100)) : 0;
        const isBest = val !== null && val === Math.max(...values.filter((v): v is number => v !== null)) && val > 0;
        return (
          <div key={i} className="flex items-center gap-2">
            <div className="flex-1">
              <p className={cn('text-sm font-semibold tabular-nums', isBest && 'text-primary')}>
                {val === null ? '—' : formatFn(val)}
              </p>
              <div className="mt-1 h-1 rounded-full bg-muted overflow-hidden">
                <div
                  className={cn('h-full rounded-full transition-all duration-500', MANAGER_COLORS[i].bar)}
                  style={{ width: `${pct}%` }}
                />
              </div>
            </div>
            {isBest && values.length > 1 && (
              <Badge variant="outline" className="shrink-0 h-5 px-1 text-[10px] font-semibold text-primary border-primary/30">
                Top
              </Badge>
            )}
          </div>
        );
      })}
    </div>
  );
}

function ManagerCard({
  manager,
  color,
  timeSeries,
  index,
}: {
  manager: ManagerRanking;
  color: typeof MANAGER_COLORS[number];
  timeSeries: { date: string; revenue: number; grossProfit: number; deals: number }[];
  index: number;
}) {
  const animatedRevenue = useCountUp(manager.revenue);
  const animatedProfit = useCountUp(manager.grossProfit);
  const animatedSales = useCountUp(manager.salesCount);

  return (
    <div className="animate-fade-in-up" style={{ animationDelay: `${index * 80}ms` }}>
      <Card className={cn('overflow-hidden shadow-sm transition-all duration-200 hover:shadow-md', 'ring-1', color.ring, 'ring-opacity-20')}>
        <div className={cn('flex items-center gap-3 px-5 py-4', color.bg)}>
          <Avatar className={cn('h-11 w-11 border-2', color.ring, 'ring-offset-1 ring-offset-card')}>
            <AvatarFallback className={cn('text-sm font-bold', color.bg, color.text)}>
              {manager.initials}
            </AvatarFallback>
          </Avatar>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-bold truncate">{manager.name}</p>
            <p className="text-xs text-muted-foreground">
              {manager.salesCount} deals · {formatPlainPercent(manager.margin)} margin
            </p>
          </div>
        </div>
        <CardContent className="p-5 space-y-4">
          <div>
            <p className="text-xs font-medium text-muted-foreground mb-1.5">Revenue Trend</p>
            <MiniAreaChart data={timeSeries} color={color.stroke} dataKey="revenue" formatFn={(v) => formatCurrency(v, true)} />
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div>
              <p className="text-xs text-muted-foreground">Revenue</p>
              <p className="text-lg font-bold tabular-nums">{formatCurrency(animatedRevenue, true)}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Gross Profit</p>
              <p className="text-lg font-bold tabular-nums">{formatCurrency(animatedProfit, true)}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Sales</p>
              <p className="text-lg font-bold tabular-nums">{Math.round(animatedSales)}</p>
            </div>
          </div>
          <div>
            <p className="text-xs font-medium text-muted-foreground mb-1.5">Profit Trend</p>
            <MiniAreaChart data={timeSeries} color={color.stroke} dataKey="grossProfit" formatFn={(v) => formatCurrency(v, true)} />
          </div>
          <div>
            <p className="text-xs font-medium text-muted-foreground mb-1.5">Deal Volume</p>
            <MiniAreaChart data={timeSeries} color={color.stroke} dataKey="deals" formatFn={(v) => String(Math.round(v))} />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function ComparisonSkeleton() {
  return (
    <div className="grid grid-cols-3 gap-4">
      {Array.from({ length: 3 }).map((_, i) => (
        <Card key={i} className="overflow-hidden shadow-sm">
          <div className="flex items-center gap-3 px-5 py-4 bg-muted/30">
            <Skeleton className="h-11 w-11 rounded-full" />
            <div className="space-y-1.5">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-3 w-16" />
            </div>
          </div>
          <CardContent className="p-5 space-y-4">
            <div className="space-y-1.5">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-14 w-full rounded-lg shimmer-pulse" style={{ animationDelay: `${i * 80}ms` }} />
            </div>
            <div className="grid grid-cols-3 gap-3">
              {Array.from({ length: 3 }).map((_, j) => (
                <div key={j} className="space-y-1">
                  <Skeleton className="h-3 w-12" />
                  <Skeleton className="h-6 w-16" />
                </div>
              ))}
            </div>
            <div className="space-y-1.5">
              <Skeleton className="h-3 w-16" />
              <Skeleton className="h-14 w-full rounded-lg shimmer-pulse" style={{ animationDelay: `${i * 80 + 40}ms` }} />
            </div>
            <div className="space-y-1.5">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-14 w-full rounded-lg shimmer-pulse" style={{ animationDelay: `${i * 80 + 80}ms` }} />
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

export function ManagerComparison({ managers, loading = false }: ManagerComparisonProps) {
  const [selectedIds, setSelectedIds] = useState<string[]>(() => managers.slice(0, 3).map(m => m.id));

  const selectedManagers = useMemo(
    () => selectedIds.map((id) => managers.find((m) => m.id === id)).filter(Boolean) as ManagerRanking[],
    [selectedIds, managers],
  );

  const timeSeriesData = useMemo(
    () => selectedManagers.map((m) => m.timeSeries.map(point => ({ ...point, deals: point.salesCount }))),
    [selectedManagers],
  );

  const toggleManager = (id: string) => {
    setSelectedIds((prev) => {
      if (prev.includes(id)) {
        if (prev.length <= 2) return prev;
        return prev.filter((x) => x !== id);
      }
      if (prev.length >= 3) return prev;
      return [...prev, id];
    });
  };

  const maxRevenue = Math.max(...selectedManagers.map((m) => m.revenue), 1);
  const maxProfit = Math.max(...selectedManagers.map((m) => m.grossProfit), 1);
  const maxSales = Math.max(...selectedManagers.map((m) => m.salesCount), 1);
  const maxAvgCheck = Math.max(...selectedManagers.map((m) => m.avgCheck), 1);
  const maxMargin = Math.max(...selectedManagers.map((m) => m.margin), 1);

  return (
    <div className="space-y-4">
      {/* Selector bar */}
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-bold tracking-tight">Manager Comparison</h2>
          <p className="mt-0.5 text-sm text-muted-foreground">
            Select 2–3 managers to compare side-by-side
          </p>
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" size="sm" className="h-9 gap-2 min-w-[200px] justify-between">
              <span className="flex items-center gap-2">
                <Users className="h-4 w-4 text-muted-foreground" />
                <span className="truncate">
                  {selectedManagers.map((m) => m.name.split(' ')[0]).join(', ')}
                </span>
              </span>
              <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-[240px]">
            {managers.map((m) => {
              const isSelected = selectedIds.includes(m.id);
              const isDisabled = !isSelected && selectedIds.length >= 3;
              return (
                <DropdownMenuCheckboxItem
                  key={m.id}
                  checked={isSelected}
                  disabled={isDisabled}
                  onCheckedChange={() => toggleManager(m.id)}
                  className="gap-2"
                >
                  <Avatar className="h-6 w-6">
                    <AvatarFallback className="text-[10px] font-semibold bg-muted">
                      {m.initials}
                    </AvatarFallback>
                  </Avatar>
                  {m.name}
                </DropdownMenuCheckboxItem>
              );
            })}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {loading ? (
        <ComparisonSkeleton />
      ) : selectedManagers.length < 2 ? (
        <Card className="shadow-sm">
          <CardContent className="flex flex-col items-center justify-center py-20 text-center">
            <div className="relative">
              <div className="absolute inset-0 rounded-full bg-primary/5 blur-xl" />
              <div className="relative flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-muted to-muted/50 ring-1 ring-border/50 animate-empty-float">
                <Users className="h-7 w-7 text-muted-foreground" />
              </div>
            </div>
            <p className="mt-5 text-sm font-semibold text-foreground">Select at least 2 managers</p>
            <p className="mt-1.5 max-w-xs text-xs leading-relaxed text-muted-foreground">
              Choose 2 or 3 managers from the dropdown above to see a side-by-side comparison.
            </p>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Side-by-side manager cards */}
          <div className="grid grid-cols-3 gap-4">
            {selectedManagers.map((m, i) => (
              <ManagerCard
                key={m.id}
                manager={m}
                color={MANAGER_COLORS[i]}
                timeSeries={timeSeriesData[i]}
                index={i}
              />
            ))}
          </div>

          {/* Comparison table */}
          <Card className="shadow-sm">
            <CardContent className="p-5">
              <div className="grid grid-cols-[140px_1fr_1fr_1fr] gap-3 pb-3 border-b">
                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                  Metric
                </p>
                {selectedManagers.map((m, i) => (
                  <div key={m.id} className="flex items-center gap-2">
                    <Avatar className={cn('h-6 w-6 border', MANAGER_COLORS[i].ring, 'ring-1 ring-opacity-30')}>
                      <AvatarFallback className={cn('text-[10px] font-bold', MANAGER_COLORS[i].bg, MANAGER_COLORS[i].text)}>
                        {m.initials}
                      </AvatarFallback>
                    </Avatar>
                    <span className="text-xs font-semibold truncate">{m.name}</span>
                  </div>
                ))}
              </div>
              <MetricRow
                icon={<DollarSign className="h-3.5 w-3.5" />}
                label="Revenue"
                values={selectedManagers.map((m) => m.revenue)}
                formatFn={(v) => formatCurrency(v, true)}
                maxVal={maxRevenue}
              />
              <MetricRow
                icon={<TrendingUp className="h-3.5 w-3.5" />}
                label="Gross Profit"
                values={selectedManagers.map((m) => m.grossProfit)}
                formatFn={(v) => formatCurrency(v, true)}
                maxVal={maxProfit}
              />
              <MetricRow
                icon={<Receipt className="h-3.5 w-3.5" />}
                label="Sales Count"
                values={selectedManagers.map((m) => m.salesCount)}
                formatFn={(v) => String(v)}
                maxVal={maxSales}
              />
              <MetricRow
                icon={<Target className="h-3.5 w-3.5" />}
                label="Avg Check"
                values={selectedManagers.map((m) => m.avgCheck)}
                formatFn={(v) => formatCurrency(v, true)}
                maxVal={maxAvgCheck}
              />
              <MetricRow
                icon={<Percent className="h-3.5 w-3.5" />}
                label="Margin"
                values={selectedManagers.map((m) => m.margin)}
                formatFn={(v) => formatPlainPercent(v)}
                maxVal={maxMargin}
              />
              <MetricRow
                icon={<TrendingUp className="h-3.5 w-3.5" />}
                label="GP Change"
                values={selectedManagers.map((m) => m.grossProfitChange)}
                formatFn={(v) => formatPercent(v)}
                maxVal={Math.max(...selectedManagers.map((m) => Math.abs(m.grossProfitChange ?? 0)), 1)}
              />
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
