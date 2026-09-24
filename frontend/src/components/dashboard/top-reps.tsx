import { TrendingUp, TrendingDown } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';
import { formatCurrency, formatPercent } from '@/lib/format';
import type { SalesRep } from '@/lib/mockData';

interface TopRepsProps {
  reps: SalesRep[];
}

export function TopReps({ reps }: TopRepsProps) {
  const maxRevenue = Math.max(...reps.map((r) => r.revenue));
  return (
    <Card className="shadow-sm">
      <CardHeader className="pb-4">
        <CardTitle className="text-base font-semibold">Top Sales Reps</CardTitle>
        <CardDescription>Ranked by revenue this period</CardDescription>
      </CardHeader>
      <CardContent className="space-y-1">
        {reps.map((rep, i) => {
          const pct = Math.round((rep.revenue / maxRevenue) * 100);
          return (
            <div
              key={rep.name}
              className="flex items-center gap-3 rounded-lg px-2 py-2 transition-colors hover:bg-muted/50"
            >
              <span className="w-5 text-center text-sm font-bold text-muted-foreground">
                {i + 1}
              </span>
              <Avatar className="h-9 w-9 border">
                <AvatarFallback className="bg-primary/10 text-xs font-semibold text-primary">
                  {rep.initials}
                </AvatarFallback>
              </Avatar>
              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between gap-2">
                  <p className="truncate text-sm font-medium">{rep.name}</p>
                  <p className="shrink-0 text-sm font-semibold">{formatCurrency(rep.revenue, true)}</p>
                </div>
                <div className="mt-1 flex items-center gap-2">
                  <Progress value={pct} className="h-1.5" />
                  <span className="text-xs text-muted-foreground">{rep.deals} deals</span>
                </div>
              </div>
              <div
                className={cn(
                  'flex w-16 shrink-0 items-center justify-end gap-0.5 text-xs font-semibold',
                  rep.trend >= 0 ? 'text-success' : 'text-destructive',
                )}
              >
                {rep.trend >= 0 ? (
                  <TrendingUp className="h-3 w-3" />
                ) : (
                  <TrendingDown className="h-3 w-3" />
                )}
                {formatPercent(rep.trend)}
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
