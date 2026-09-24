import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip } from 'recharts';
import { Layers } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { formatCurrency } from '@/lib/format';
import { EmptyState } from '@/components/dashboard/empty-state';
import type { CategoryProfit } from '@/lib/analytics-api';

interface CategoryProfitChartProps {
    data: CategoryProfit[];
    totalRevenue: number;
    loading?: boolean;
}

function PieTooltip({ active, payload }: any) {
    if (!active || !payload?.length) return null;
    const item = payload[0].payload as CategoryProfit;
    return (
        <div className="rounded-lg border bg-popover p-3 shadow-lg">
            <p className="mb-1.5 text-xs font-semibold">{item.name}</p>
            <div className="flex flex-col gap-1">
                <div className="flex items-center justify-between gap-6">
                    <span className="text-xs text-muted-foreground">Revenue</span>
                    <span className="text-xs font-semibold">{formatCurrency(item.revenue, true)}</span>
                </div>
                <div className="flex items-center justify-between gap-6">
                    <span className="text-xs text-muted-foreground">Gross Profit</span>
                    <span className="text-xs font-semibold">{formatCurrency(item.grossProfit, true)}</span>
                </div>
            </div>
        </div>
    );
}

function SkeletonState() {
    return (
        <div className="p-6">
            <div className="space-y-2">
                <Skeleton className="h-5 w-40" />
                <Skeleton className="h-3.5 w-28" />
            </div>
            <div className="mt-6 flex items-center gap-8">
                <div className="relative h-[200px] w-[200px] shrink-0">
                    <Skeleton className="h-[200px] w-[200px] rounded-full shimmer-pulse" />
                    <div className="absolute inset-0 flex items-center justify-center">
                        <Skeleton className="h-14 w-14 rounded-full" />
                    </div>
                </div>
                <div className="flex-1 space-y-4">
                    {Array.from({ length: 5 }).map((_, i) => (
                        <div key={i} className="space-y-2 shimmer-pulse" style={{ animationDelay: `${i * 100}ms` }}>
                            <div className="flex items-center justify-between">
                                <div className="flex items-center gap-2.5">
                                    <Skeleton className="h-3 w-3 rounded-sm" />
                                    <Skeleton className="h-4 w-24" />
                                </div>
                                <Skeleton className="h-4 w-20" />
                            </div>
                            <div className="pl-5">
                                <Skeleton className="h-3.5 w-28" />
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}

export function CategoryProfitChart({ data, totalRevenue, loading = false }: CategoryProfitChartProps) {
    if (loading) {
        return (
            <Card className="shadow-sm h-full">
                <SkeletonState />
            </Card>
        );
    }

    return (
        <Card className="shadow-sm h-full">
            <CardHeader className="pb-5">
                <CardTitle className="text-base font-semibold">Sales / Profit by Categories</CardTitle>
                <CardDescription>Revenue and gross profit share</CardDescription>
            </CardHeader>

            <CardContent className="pt-0">
                {data.length === 0 ? (
                    <div className="flex h-[280px] items-center justify-center">
                        <EmptyState
                            icon={<Layers className="h-8 w-8 text-muted-foreground" />}
                            title="No category data available"
                            description="Category breakdown will appear here once sales are recorded."
                        />
                    </div>
                ) : (
                    <div className="flex items-center gap-8 min-h-[260px]">
                        {/* Pie Chart */}
                        <div className="relative h-[200px] w-[200px] shrink-0">
                            <ResponsiveContainer width="100%" height="100%">
                                <PieChart>
                                    <Pie
                                        data={data}
                                        dataKey="revenue"
                                        nameKey="name"
                                        innerRadius={58}
                                        outerRadius={92}
                                        paddingAngle={3}
                                        strokeWidth={0}
                                    >
                                        {data.map((entry, i) => (
                                            <Cell key={i} fill={entry.fill} />
                                        ))}
                                    </Pie>
                                    <Tooltip content={<PieTooltip />} />
                                </PieChart>
                            </ResponsiveContainer>

                            {/* Center label */}
                            <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
                                <p className="text-[11px] font-medium text-muted-foreground">Revenue</p>
                                <p className="text-base font-bold tracking-tight">
                                    {formatCurrency(totalRevenue, true)}
                                </p>
                            </div>
                        </div>

                        {/* Legend */}
                        <div className="flex-1 space-y-3.5">
                            {data.map((cat) => {
                                const revPct = cat.revenueShare.toFixed(1);
                                const profitPct =
                                    cat.grossProfitShare === null
                                        ? '—'
                                        : `${cat.grossProfitShare.toFixed(1)}%`;

                                return (
                                    <div key={cat.name} className="space-y-1">
                                        <div className="flex items-center justify-between gap-4">
                                            <div className="flex items-center gap-2.5 min-w-0">
                        <span
                            className="h-3 w-3 rounded-sm shrink-0"
                            style={{ background: cat.fill }}
                        />
                                                <span className="text-sm font-medium truncate">{cat.name}</span>
                                            </div>
                                            <div className="flex items-center gap-3 shrink-0">
                        <span className="text-sm font-semibold">
                          {formatCurrency(cat.revenue, true)}
                        </span>
                                                <span className="w-12 text-right text-xs text-muted-foreground">
                          {revPct}%
                        </span>
                                            </div>
                                        </div>
                                        <div className="flex items-center justify-between gap-4 pl-[22px]">
                      <span className="text-xs text-muted-foreground">
                        Profit {formatCurrency(cat.grossProfit, true)}
                      </span>
                                            <span className="w-12 text-right text-xs text-muted-foreground/70">
                        {profitPct}
                      </span>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}
