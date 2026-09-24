import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { formatCurrency } from '@/lib/format';
import type { generateDailyData } from '@/lib/mockData';

type DailyPoint = ReturnType<typeof generateDailyData>[number];

interface RevenueChartProps {
  data: DailyPoint[];
  totalRevenue: number;
  revenueDelta: number;
}

function ChartTooltip({ active, payload, label }: any) {
  if (!active || !payload?.length) return null;
  const revenue = payload.find((p: any) => p.dataKey === 'revenue')?.value ?? 0;
  const target = payload.find((p: any) => p.dataKey === 'target')?.value ?? 0;
  return (
    <div className="rounded-lg border bg-popover p-3 shadow-lg">
      <p className="mb-1.5 text-xs font-semibold text-foreground">{label}</p>
      <div className="flex flex-col gap-1">
        <div className="flex items-center justify-between gap-6">
          <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <span className="h-2 w-2 rounded-full bg-primary" />
            Revenue
          </span>
          <span className="text-xs font-semibold">{formatCurrency(revenue)}</span>
        </div>
        <div className="flex items-center justify-between gap-6">
          <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <span className="h-2 w-2 rounded-full bg-muted-foreground/40" />
            Target
          </span>
          <span className="text-xs font-semibold text-muted-foreground">{formatCurrency(target)}</span>
        </div>
      </div>
    </div>
  );
}

export function RevenueChart({ data, totalRevenue, revenueDelta }: RevenueChartProps) {
  return (
    <Card className="shadow-sm">
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-4">
        <div>
          <CardTitle className="text-base font-semibold">Revenue Overview</CardTitle>
          <CardDescription className="mt-1">
            Daily revenue vs target
          </CardDescription>
        </div>
        <div className="flex items-center gap-3">
          <div className="text-right">
            <p className="text-lg font-bold tracking-tight">{formatCurrency(totalRevenue, true)}</p>
            <Badge variant={revenueDelta >= 0 ? 'default' : 'destructive'} className="mt-0.5">
              {revenueDelta >= 0 ? '+' : ''}{revenueDelta.toFixed(1)}%
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="pl-2 pr-4 pb-4">
        <ResponsiveContainer width="100%" height={260}>
          <AreaChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
            <defs>
              <linearGradient id="revGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="hsl(var(--chart-1))" stopOpacity={0.28} />
                <stop offset="100%" stopColor="hsl(var(--chart-1))" stopOpacity={0} />
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
              tickFormatter={(v) => formatCurrency(v, true)}
              width={56}
            />
            <Tooltip content={<ChartTooltip />} />
            <Area
              type="monotone"
              dataKey="target"
              stroke="hsl(var(--muted-foreground))"
              strokeWidth={1.5}
              strokeDasharray="5 4"
              fill="none"
              dot={false}
            />
            <Area
              type="monotone"
              dataKey="revenue"
              stroke="hsl(var(--chart-1))"
              strokeWidth={2.5}
              fill="url(#revGrad)"
              dot={false}
              activeDot={{ r: 4, fill: 'hsl(var(--chart-1))', strokeWidth: 2, stroke: 'hsl(var(--card))' }}
            />
          </AreaChart>
        </ResponsiveContainer>
      </CardContent>
    </Card>
  );
}
