import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip } from 'recharts';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { formatCurrency } from '@/lib/format';
import type { getCategoryBreakdown } from '@/lib/mockData';

type CategoryItem = ReturnType<typeof getCategoryBreakdown>[number];

interface CategoryBreakdownProps {
  data: CategoryItem[];
}

function PieTooltip({ active, payload }: any) {
  if (!active || !payload?.length) return null;
  const item = payload[0].payload as CategoryItem;
  const total = item.value;
  return (
    <div className="rounded-lg border bg-popover p-3 shadow-lg">
      <p className="mb-1 text-xs font-semibold">{item.name}</p>
      <p className="text-sm font-bold">{formatCurrency(total, true)}</p>
    </div>
  );
}

export function CategoryBreakdown({ data }: CategoryBreakdownProps) {
  const total = data.reduce((sum, d) => sum + d.value, 0);
  return (
    <Card className="shadow-sm">
      <CardHeader className="pb-4">
        <CardTitle className="text-base font-semibold">Revenue by Segment</CardTitle>
        <CardDescription>Distribution across customer tiers</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex items-center gap-6">
          <div className="relative h-[140px] w-[140px] shrink-0">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={data}
                  dataKey="value"
                  nameKey="name"
                  innerRadius={42}
                  outerRadius={64}
                  paddingAngle={2}
                  strokeWidth={0}
                >
                  {data.map((entry, i) => (
                    <Cell key={i} fill={entry.fill} />
                  ))}
                </Pie>
                <Tooltip content={<PieTooltip />} />
              </PieChart>
            </ResponsiveContainer>
            <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
              <p className="text-[10px] font-medium text-muted-foreground">Total</p>
              <p className="text-sm font-bold">{formatCurrency(total, true)}</p>
            </div>
          </div>
          <div className="flex-1 space-y-2.5">
            {data.map((cat) => {
              const pct = ((cat.value / total) * 100).toFixed(1);
              return (
                <div key={cat.name} className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <span className="h-2.5 w-2.5 rounded-sm" style={{ background: cat.fill }} />
                    <span className="text-sm font-medium">{cat.name}</span>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-sm font-semibold">{formatCurrency(cat.value, true)}</span>
                    <span className="w-10 text-right text-xs text-muted-foreground">{pct}%</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
