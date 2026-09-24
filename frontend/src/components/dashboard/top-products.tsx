import { Package } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';
import { formatCurrency, formatPlainPercent } from '@/lib/format';
import { EmptyState } from '@/components/dashboard/empty-state';
import type { TopProduct } from '@/lib/analytics-api';

interface TopProductsProps {
  products: TopProduct[];
  loading?: boolean;
}

function SkeletonState() {
  return (
    <div className="space-y-1 p-6 pt-0">
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="flex items-center gap-3 py-2.5 shimmer-pulse" style={{ animationDelay: `${i * 70}ms` }}>
          <Skeleton className="h-5 w-5 rounded" />
          <div className="flex-1 space-y-1.5">
            <div className="flex items-center gap-2">
              <Skeleton className="h-4 w-32" />
              <Skeleton className="h-5 w-14 rounded-md" />
            </div>
            <div className="flex items-center gap-2">
              <Skeleton className="h-1.5 flex-1 rounded-full" />
              <Skeleton className="h-3 w-12" />
              <Skeleton className="h-3 w-10" />
            </div>
          </div>
          <Skeleton className="h-4 w-16" />
        </div>
      ))}
    </div>
  );
}



const CATEGORY_COLORS: Record<string, string> = {
  Enterprise: 'bg-chart-1/10 text-chart-1',
  'Mid-Market': 'bg-chart-2/10 text-chart-2',
  SMB: 'bg-chart-3/10 text-chart-3',
  Startup: 'bg-chart-4/10 text-chart-4',
};

export function TopProducts({ products, loading = false }: TopProductsProps) {
  if (loading) {
    return (
      <Card className="shadow-sm">
        <CardHeader className="pb-4">
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-3 w-20" />
        </CardHeader>
        <SkeletonState />
      </Card>
    );
  }

  const maxRevenue = Math.max(...products.map((p) => p.revenue), 1);

  return (
    <Card className="shadow-sm">
      <CardHeader className="pb-4">
        <CardTitle className="text-base font-semibold">Top Products</CardTitle>
        <CardDescription>Ranked by revenue and profitability</CardDescription>
      </CardHeader>
      <CardContent className="space-y-0.5">
        {products.length === 0 ? (
          <div className="flex h-[220px] items-center justify-center">
            <EmptyState
              icon={<Package className="h-7 w-7 text-muted-foreground" />}
              title="No product data available"
              description="Top products will appear here once sales are recorded for this period."
            />
          </div>
        ) : (
          products.map((product, i) => {
            const pct = Math.round((product.revenue / maxRevenue) * 100);
            const colorClass = CATEGORY_COLORS[product.category] ?? 'bg-muted text-muted-foreground';
            return (
              <div
                key={product.id}
                className="group flex items-center gap-3 rounded-lg px-2 py-2.5 transition-colors hover:bg-muted/40"
              >
                <span className="w-5 text-center text-sm font-bold text-muted-foreground">
                  {i + 1}
                </span>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      <p className="truncate text-sm font-medium">{product.name}</p>
                      <Badge variant="outline" className={cn('h-5 px-1.5 text-[10px] font-medium', colorClass)}>
                        {product.category}
                      </Badge>
                    </div>
                    <p className="shrink-0 text-sm font-semibold tabular-nums">
                      {formatCurrency(product.revenue, true)}
                    </p>
                  </div>
                  <div className="mt-1.5 flex items-center gap-2">
                    <Progress value={pct} className="h-1.5" />
                    <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                      Profit {formatCurrency(product.grossProfit, true)}
                    </span>
                    <span className="shrink-0 text-xs font-medium tabular-nums">
                      {formatPlainPercent(product.margin)}
                    </span>
                  </div>
                </div>
              </div>
            );
          })
        )}
      </CardContent>
    </Card>
  );
}
