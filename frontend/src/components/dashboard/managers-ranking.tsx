import { useState, useMemo } from 'react';
import { ArrowUpRight, ArrowDownRight, Trophy, Users, ChevronLeft, ChevronRight } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableHeader,
  TableBody,
  TableHead,
  TableRow,
  TableCell,
} from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import { formatCurrency, formatPlainPercent, formatPercent } from '@/lib/format';
import type { ManagerRanking, RankingMode } from '@/lib/analytics-api';
import { EmptyState } from '@/components/dashboard/empty-state';

const RANK_STYLES = [
  'bg-gradient-to-br from-amber-400/20 to-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/30',
  'bg-gradient-to-br from-slate-300/20 to-slate-400/10 text-slate-500 dark:text-slate-300 border-slate-400/30',
  'bg-gradient-to-br from-orange-400/20 to-orange-600/10 text-orange-600 dark:text-orange-400 border-orange-500/30',
];

const PAGE_SIZE_OPTIONS = [3, 6] as const;
const DEFAULT_PAGE_SIZE = 3;

function RankBadge({ rank }: { rank: number }) {
  if (rank <= 3) {
    return (
        <div
            className={cn(
                'flex h-8 w-8 items-center justify-center rounded-lg border text-sm font-bold',
                RANK_STYLES[rank - 1],
            )}
        >
          {rank === 1 ? <Trophy className="h-4 w-4" /> : rank}
        </div>
    );
  }
  return (
      <div className="flex h-8 w-8 items-center justify-center rounded-lg border border-border bg-muted/50 text-sm font-semibold text-muted-foreground">
        {rank}
      </div>
  );
}

function ChangeIndicator({ change }: { change: number | null }) {
  if (change === null) return <span className="text-muted-foreground">—</span>;
  const positive = change >= 0;
  return (
      <span
          className={cn(
              'inline-flex items-center gap-0.5 text-xs font-semibold',
              positive ? 'text-success' : 'text-destructive',
          )}
      >
      {positive ? <ArrowUpRight className="h-3 w-3" /> : <ArrowDownRight className="h-3 w-3" />}
        {formatPercent(change)}
    </span>
  );
}

function RankingSkeleton() {
  return (
      <div className="space-y-0">
        <div className="flex items-center gap-3 border-b px-6 py-2.5">
          {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-3 flex-1" style={{ maxWidth: 60 + i * 10 }} />
          ))}
        </div>
        {Array.from({ length: 6 }).map((_, i) => (
            <div
                key={i}
                className="flex items-center gap-3 border-b px-6 py-3 shimmer-pulse"
                style={{ animationDelay: `${i * 80}ms` }}
            >
              <Skeleton className="h-8 w-8 rounded-lg" />
              <Skeleton className="h-9 w-9 rounded-full" />
              <div className="flex-1 space-y-1.5">
                <Skeleton className="h-3.5 w-28" />
                <Skeleton className="h-2.5 w-16" />
              </div>
              <Skeleton className="h-3.5 w-12" />
              <Skeleton className="h-3.5 w-16" />
              <div className="flex flex-col items-end gap-1">
                <Skeleton className="h-3.5 w-16" />
                <Skeleton className="h-1 w-20 rounded-full" />
              </div>
              <Skeleton className="h-3.5 w-14" />
              <Skeleton className="h-3.5 w-12" />
            </div>
        ))}
      </div>
  );
}

interface ManagersRankingProps {
  managers: ManagerRanking[];
  mode: RankingMode;
  onModeChange: (mode: RankingMode) => void;
  loading?: boolean;
}

export function ManagersRanking({
                                  managers,
                                  mode,
                                  onModeChange,
                                  loading = false,
                                }: ManagersRankingProps) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const maxGrossProfit = Math.max(...managers.map((m) => m.grossProfit), 1);
  const maxAvgCheck = Math.max(...managers.map((m) => m.avgCheck), 1);

  const totalPages = Math.max(1, Math.ceil(managers.length / pageSize));
  const safePage = Math.min(page, totalPages);

  const paginatedManagers = useMemo(() => {
    const start = (safePage - 1) * pageSize;
    return managers.slice(start, start + pageSize);
  }, [managers, safePage, pageSize]);

  // Сброс страницы при смене режима / данных / размера страницы
  const handleModeChange = (v: RankingMode) => {
    onModeChange(v);
    setPage(1);
  };

  const handlePageSizeChange = (value: string) => {
    setPageSize(Number(value));
    setPage(1);
  };

  return (
      <Card className="shadow-sm">
        <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-4">
          <div>
            <CardTitle className="text-base font-semibold">Managers Ranking</CardTitle>
            <CardDescription className="mt-1">
              Performance ranked by {mode === 'grossProfit' ? 'gross profit' : 'average check'}
            </CardDescription>
          </div>
          <Tabs value={mode} onValueChange={(v) => handleModeChange(v as RankingMode)}>
            <TabsList className="h-9">
              <TabsTrigger value="grossProfit" className="text-xs">
                Gross Profit
              </TabsTrigger>
              <TabsTrigger value="avgCheck" className="text-xs">
                Average Check
              </TabsTrigger>
            </TabsList>
          </Tabs>
        </CardHeader>

        <CardContent className="px-0 pb-0">
          {loading ? (
              <RankingSkeleton />
          ) : managers.length === 0 ? (
              <EmptyState
                  icon={<Users className="h-7 w-7 text-muted-foreground" />}
                  title="No manager data available"
                  description="Manager rankings will appear here once sales data is recorded for this period."
                  className="py-16"
              />
          ) : (
              <>
                <Table>
                  <TableHeader>
                    <TableRow className="hover:bg-transparent">
                      <TableHead className="pl-6 w-12">#</TableHead>
                      <TableHead>Manager</TableHead>
                      <TableHead className="text-right">Sales</TableHead>
                      <TableHead className="text-right">Revenue</TableHead>
                      <TableHead className="text-right">
                        {mode === 'grossProfit' ? 'Gross Profit' : 'Avg Check'}
                      </TableHead>
                      <TableHead className="text-right">
                        {mode === 'grossProfit' ? 'Avg Check' : 'Gross Profit'}
                      </TableHead>
                      <TableHead className="text-right">Margin</TableHead>
                      <TableHead className="pr-6 text-right">Change</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {paginatedManagers.map((m, i) => {
                      // Глобальный ранг (не сбрасывается на каждой странице)
                      const rank = (safePage - 1) * pageSize + i + 1;
                      const isTop3 = rank <= 3;
                      const sortValue = mode === 'grossProfit' ? m.grossProfit : m.avgCheck;
                      const sortMax = mode === 'grossProfit' ? maxGrossProfit : maxAvgCheck;
                      const barWidth = Math.max(0, Math.min(100, (sortValue / sortMax) * 100));
                      const change = mode === 'grossProfit' ? m.grossProfitChange : m.avgCheckChange;

                      return (
                          <TableRow
                              key={m.id}
                              className={cn(
                                  'group transition-all duration-300',
                                  isTop3 ? 'bg-muted/30' : '',
                                  'hover:bg-muted/50',
                              )}
                          >
                            <TableCell className="pl-6 py-3">
                              <RankBadge rank={rank} />
                            </TableCell>
                            <TableCell className="py-3">
                              <div className="flex items-center gap-2.5">
                                <Avatar
                                    className={cn(
                                        'h-9 w-9 border transition-shadow',
                                        isTop3 && 'ring-1 ring-primary/20',
                                    )}
                                >
                                  <AvatarFallback
                                      className={cn(
                                          'text-xs font-semibold',
                                          isTop3
                                              ? 'bg-primary/15 text-primary'
                                              : 'bg-muted text-muted-foreground',
                                      )}
                                  >
                                    {m.initials}
                                  </AvatarFallback>
                                </Avatar>
                                <div>
                                  <p className="text-sm font-medium leading-tight">{m.name}</p>
                                  <p className="text-xs text-muted-foreground leading-tight mt-0.5">
                                    {m.salesCount} deals closed
                                  </p>
                                </div>
                              </div>
                            </TableCell>
                            <TableCell className="py-3 text-right text-sm tabular-nums">
                              {m.salesCount}
                            </TableCell>
                            <TableCell className="py-3 text-right text-sm font-medium tabular-nums">
                              {formatCurrency(m.revenue, true)}
                            </TableCell>
                            <TableCell className="py-3 text-right">
                              <div className="flex flex-col items-end gap-1">
                          <span className="text-sm font-semibold tabular-nums">
                            {mode === 'grossProfit'
                                ? formatCurrency(m.grossProfit, true)
                                : formatCurrency(m.avgCheck, true)}
                          </span>
                                <div className="h-1 w-20 rounded-full bg-muted overflow-hidden">
                                  <div
                                      className={cn(
                                          'h-full rounded-full transition-all duration-500',
                                          isTop3 ? 'bg-primary' : 'bg-primary/50',
                                      )}
                                      style={{ width: `${barWidth}%` }}
                                  />
                                </div>
                              </div>
                            </TableCell>
                            <TableCell className="py-3 text-right text-sm text-muted-foreground tabular-nums">
                              {mode === 'grossProfit'
                                  ? formatCurrency(m.avgCheck, true)
                                  : formatCurrency(m.grossProfit, true)}
                            </TableCell>
                            <TableCell className="py-3 text-right text-sm tabular-nums">
                              {formatPlainPercent(m.margin)}
                            </TableCell>
                            <TableCell className="pr-6 py-3 text-right">
                              <ChangeIndicator change={change} />
                            </TableCell>
                          </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>

                {/* Pagination footer */}
                <div className="flex flex-col gap-3 border-t px-6 py-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <span>
                  Showing{' '}
                  <span className="font-medium text-foreground">
                    {(safePage - 1) * pageSize + 1}
                  </span>
                  –
                  <span className="font-medium text-foreground">
                    {Math.min(safePage * pageSize, managers.length)}
                  </span>{' '}
                  of <span className="font-medium text-foreground">{managers.length}</span>
                </span>

                    <Select value={String(pageSize)} onValueChange={handlePageSizeChange}>
                      <SelectTrigger className="h-8 w-[70px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {PAGE_SIZE_OPTIONS.map((size) => (
                            <SelectItem key={size} value={String(size)}>
                              {size}
                            </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <span>per page</span>
                  </div>

                  <div className="flex items-center gap-1">
                    <Button
                        variant="outline"
                        size="icon"
                        className="h-8 w-8"
                        disabled={safePage <= 1}
                        onClick={() => setPage((p) => Math.max(1, p - 1))}
                    >
                      <ChevronLeft className="h-4 w-4" />
                      <span className="sr-only">Previous page</span>
                    </Button>

                    <div className="flex items-center gap-1 px-2 text-sm tabular-nums">
                      <span className="font-medium">{safePage}</span>
                      <span className="text-muted-foreground">/</span>
                      <span className="text-muted-foreground">{totalPages}</span>
                    </div>

                    <Button
                        variant="outline"
                        size="icon"
                        className="h-8 w-8"
                        disabled={safePage >= totalPages}
                        onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    >
                      <ChevronRight className="h-4 w-4" />
                      <span className="sr-only">Next page</span>
                    </Button>
                  </div>
                </div>
              </>
          )}
        </CardContent>
      </Card>
  );
}
