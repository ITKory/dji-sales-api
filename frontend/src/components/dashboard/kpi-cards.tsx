import { TrendingUp, ArrowUpRight, ArrowDownRight } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';
import { formatCurrency, formatCompactNumber, formatPlainPercent } from '@/lib/format';
import { useCountUp } from '@/hooks/use-count-up';
import type { KpiData } from '@/lib/analytics-api';

interface KpiCardProps {
  title: string;
  value: number;
  subtitle?: string;
  formatter: (v: number) => string;
  deltaPct: number | null;
  deltaUnit?: string;
  deltaAbs: string;
  sparkline: number[];
  icon: React.ReactNode;
  accent?: 'primary' | 'success' | 'warning' | 'info';
}

const ACCENT_MAP = {
  primary: 'bg-primary/10 text-primary',
  success: 'bg-success/10 text-success',
  warning: 'bg-warning/10 text-warning',
  info: 'bg-info/10 text-info',
};

const SPARK_COLORS = {
  primary: 'hsl(var(--primary))',
  success: 'hsl(var(--success))',
  warning: 'hsl(var(--warning))',
  info: 'hsl(var(--info))',
};

function Sparkline({ data, color }: { data: number[]; color: string }) {
  if (data.length < 2) return <svg viewBox="0 0 100 32" className="h-8 w-full"><circle cx="50" cy="16" r="2" fill={color} /></svg>;
  const w = 100;
  const h = 32;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  const pts = data.map((v, i) => {
    const x = (i / (data.length - 1)) * w;
    const y = h - ((v - min) / range) * (h - 4) - 2;
    return `${x},${y}`;
  });
  const linePath = `M ${pts.join(' L ')}`;
  const areaPath = `${linePath} L ${w},${h} L 0,${h} Z`;
  const gradId = `spark-${color.replace(/[^a-z0-9]/gi, '')}`;

  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="h-8 w-full" preserveAspectRatio="none">
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.25} />
          <stop offset="100%" stopColor={color} stopOpacity={0} />
        </linearGradient>
      </defs>
      <path d={areaPath} fill={`url(#${gradId})`} />
      <path d={linePath} fill="none" stroke={color} strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function KpiCard({ title, value, subtitle, formatter, deltaPct, deltaAbs, deltaUnit = '%', sparkline, icon, accent = 'primary' }: KpiCardProps) {
  const positive = deltaPct !== null && deltaPct >= 0;
  const animatedValue = useCountUp(value);
  return (
    <Card className="group relative overflow-hidden p-5 shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:shadow-md">
      <div className="flex items-start justify-between">
        <div className="flex flex-col gap-0.5">
          <p className="text-sm font-medium text-muted-foreground">{title}</p>
          <p className="text-[26px] font-bold leading-tight tracking-tight tabular-nums">{formatter(animatedValue)}</p>
          {subtitle && <p className="text-xs text-muted-foreground">{subtitle}</p>}
        </div>
        <div className={cn('flex h-10 w-10 items-center justify-center rounded-lg transition-transform group-hover:scale-105', ACCENT_MAP[accent])}>
          {icon}
        </div>
      </div>

      <div className="mt-3 flex items-center justify-between gap-2">
        <div className="flex items-center gap-1.5">
          <span
            className={cn(
              'inline-flex items-center gap-0.5 rounded-md px-1.5 py-0.5 text-xs font-semibold',
              deltaPct === null ? 'text-muted-foreground' : positive ? 'bg-success/10 text-success' : 'bg-destructive/10 text-destructive',
            )}
          >
            {deltaPct !== null && (positive ? <ArrowUpRight className="h-3 w-3" /> : <ArrowDownRight className="h-3 w-3" />)}
            {deltaPct === null ? '—' : `${deltaPct > 0 ? '+' : ''}${deltaPct.toFixed(1)}${deltaUnit}`} 
          </span>
          <span className={cn('text-xs font-medium', positive ? 'text-success' : 'text-destructive')}>
            Previous: {deltaAbs}
          </span>
        </div>
      </div>

      <div className="mt-3 -mx-1">
        <Sparkline data={sparkline} color={SPARK_COLORS[accent]} />
      </div>
    </Card>
  );
}

function KpiCardSkeleton({ index }: { index: number }) {
  return (
    <Card className="p-5 shadow-sm">
      <div className="flex items-start justify-between">
        <div className="flex flex-col gap-1.5">
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-7 w-28" />
          <Skeleton className="h-3 w-16" />
        </div>
        <Skeleton className="h-10 w-10 rounded-lg" />
      </div>
      <div className="mt-3 flex items-center gap-2">
        <Skeleton className="h-5 w-14 rounded-md" />
        <Skeleton className="h-3 w-12" />
      </div>
      <div className="mt-3 flex items-end gap-0.5 h-8" style={{ animationDelay: `${index * 80}ms` }}>
        {Array.from({ length: 12 }).map((_, j) => (
          <Skeleton
            key={j}
            className="flex-1 rounded-sm shimmer-pulse"
            style={{ height: `${30 + Math.sin(j * 0.8 + index) * 40}%`, animationDelay: `${j * 50}ms` }}
          />
        ))}
      </div>
    </Card>
  );
}

interface KpiCardsProps {
  kpis: KpiData;
  sparklines: Record<string, number[]>;
  loading?: boolean;
}

export function KpiCards({ kpis, sparklines, loading = false }: KpiCardsProps) {
  if (loading) {
    return (
      <div className="grid grid-cols-6 gap-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <KpiCardSkeleton key={i} index={i} />
        ))}
      </div>
    );
  }

  return (
    <div className="grid grid-cols-6 gap-4">
      <KpiCard
        title="Revenue"
        value={kpis.revenue}
        formatter={(v) => formatCurrency(v, true)}
        deltaPct={kpis.revenueDelta}
        deltaAbs={formatCurrency(kpis.revenuePrev, true)}
        sparkline={sparklines.revenue}
        icon={<TrendingUp className="h-5 w-5" />}
        accent="primary"
      />
      <KpiCard
        title="Gross Profit"
        value={kpis.grossProfit}
        formatter={(v) => formatCurrency(v, true)}
        deltaPct={kpis.grossProfitDelta}
        deltaAbs={formatCurrency(kpis.grossProfitPrev, true)}
        sparkline={sparklines.grossProfit}
        icon={<TrendingUp className="h-5 w-5" />}
        accent="success"
      />
      <KpiCard
        title="Margin %"
        value={kpis.margin}
        formatter={(v) => formatPlainPercent(v)}
        deltaPct={kpis.marginDelta}
        deltaUnit="pp"
        deltaAbs={formatPlainPercent(kpis.marginPrev)}
        sparkline={sparklines.margin}
        icon={<TrendingUp className="h-5 w-5" />}
        accent="info"
      />
      <KpiCard
        title="Number of Sales"
        value={kpis.salesCount}
        formatter={(v) => formatCompactNumber(Math.round(v))}
        deltaPct={kpis.salesCountDelta}
        deltaAbs={formatCompactNumber(kpis.salesCountPrev)}
        sparkline={sparklines.salesCount}
        icon={<TrendingUp className="h-5 w-5" />}
        accent="warning"
      />
      <KpiCard
        title="Average Check"
        value={kpis.avgCheck}
        formatter={(v) => formatCurrency(v, true)}
        deltaPct={kpis.avgCheckDelta}
        deltaAbs={formatCurrency(kpis.avgCheckPrev, true)}
        sparkline={sparklines.avgCheck}
        icon={<TrendingUp className="h-5 w-5" />}
        accent="primary"
      />
      <KpiCard
        title="Best Manager"
        value={kpis.bestManagerValue}
        formatter={() => kpis.bestManager ?? '—'}
        subtitle={formatCurrency(kpis.bestManagerValue, true)}
        deltaPct={kpis.bestManagerDelta}
        deltaAbs={formatCurrency(kpis.bestManagerPrev, true)}
        sparkline={sparklines.bestManager}
        icon={<TrendingUp className="h-5 w-5" />}
        accent="success"
      />
    </div>
  );
}
