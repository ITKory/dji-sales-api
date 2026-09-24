import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Cell } from 'recharts';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { formatCurrency, formatCompactNumber } from '@/lib/format';
import type { FunnelStage } from '@/lib/mockData';

interface FunnelChartProps {
  data: FunnelStage[];
}

function FunnelTooltip({ active, payload, label }: any) {
  if (!active || !payload?.length) return null;
  const item = payload[0].payload as FunnelStage;
  return (
    <div className="rounded-lg border bg-popover p-3 shadow-lg">
      <p className="mb-1.5 text-xs font-semibold">{label}</p>
      <div className="flex flex-col gap-1 text-xs">
        <div className="flex justify-between gap-6">
          <span className="text-muted-foreground">Deals</span>
          <span className="font-semibold">{formatCompactNumber(item.count)}</span>
        </div>
        <div className="flex justify-between gap-6">
          <span className="text-muted-foreground">Pipeline Value</span>
          <span className="font-semibold">{formatCurrency(item.value, true)}</span>
        </div>
        <div className="flex justify-between gap-6">
          <span className="text-muted-foreground">Conversion</span>
          <span className="font-semibold">{item.conversion}%</span>
        </div>
      </div>
    </div>
  );
}

const BAR_COLORS = [
  'hsl(var(--chart-1))',
  'hsl(var(--chart-2))',
  'hsl(var(--chart-3))',
  'hsl(var(--chart-4))',
  'hsl(var(--chart-5))',
];

export function FunnelChart({ data }: FunnelChartProps) {
  return (
    <Card className="shadow-sm">
      <CardHeader className="pb-4">
        <CardTitle className="text-base font-semibold">Sales Funnel</CardTitle>
        <CardDescription>Pipeline conversion by stage</CardDescription>
      </CardHeader>
      <CardContent className="pl-2 pr-4 pb-4">
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
            <XAxis
              dataKey="stage"
              tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }}
              tickLine={false}
              axisLine={false}
            />
            <YAxis
              tick={{ fontSize: 11, fill: 'hsl(var(--muted-foreground))' }}
              tickLine={false}
              axisLine={false}
              tickFormatter={(v) => formatCompactNumber(v)}
              width={40}
            />
            <Tooltip content={<FunnelTooltip />} cursor={{ fill: 'hsl(var(--muted))', opacity: 0.4 }} />
            <Bar dataKey="count" radius={[6, 6, 0, 0]} maxBarSize={56}>
              {data.map((_, i) => (
                <Cell key={i} fill={BAR_COLORS[i % BAR_COLORS.length]} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
        <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 px-2">
          {data.map((stage, i) => (
            <div key={stage.stage} className="flex items-center gap-1.5">
              <span className="h-2.5 w-2.5 rounded-sm" style={{ background: BAR_COLORS[i % BAR_COLORS.length] }} />
              <span className="text-xs text-muted-foreground">{stage.stage}</span>
              <span className="text-xs font-medium">{stage.conversion}%</span>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
