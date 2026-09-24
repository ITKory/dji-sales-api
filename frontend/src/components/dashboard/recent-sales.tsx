import { Receipt, ChevronLeft, ChevronRight, Package } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
  Table,
  TableHeader,
  TableBody,
  TableHead,
  TableRow,
  TableCell,
} from '@/components/ui/table';
import { cn } from '@/lib/utils';
import { formatCurrency } from '@/lib/format';
import { EmptyState } from '@/components/dashboard/empty-state';
import type { RecentSale, SaleStatus } from '@/lib/analytics-api';

interface RecentSalesProps {
  sales: RecentSale[];
  loading?: boolean;
  pageSize?: number;
  page: number;
  totalCount: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

const STATUS_CONFIG: Record<
    SaleStatus,
    { variant: 'default' | 'destructive' | 'secondary'; dot: string; label: string }
> = {
  Paid: { variant: 'default', dot: 'bg-success', label: 'Paid' },
  Cancelled: { variant: 'secondary', dot: 'bg-muted-foreground', label: 'Cancelled' },
  Refunded: { variant: 'destructive', dot: 'bg-destructive', label: 'Refunded' },
};

function SkeletonRows({ rows }: { rows: number }) {
  return (
      <div className="space-y-0">
        <div className="flex items-center gap-3 border-b px-6 py-2.5">
          <Skeleton className="h-3 w-16" />
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-3 flex-1" />
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-3 w-16" />
          <Skeleton className="h-3 w-16" />
          <Skeleton className="h-3 w-16" />
        </div>
        {Array.from({ length: rows }).map((_, i) => (
            <div
                key={i}
                className="flex items-center gap-3 border-b px-6 py-3 shimmer-pulse"
                style={{ animationDelay: `${i * 60}ms` }}
            >
              <Skeleton className="h-3 w-20" />
              <div className="flex items-center gap-2">
                <Skeleton className="h-7 w-7 rounded-full" />
                <Skeleton className="h-3 w-20" />
              </div>
              <div className="flex-1 space-y-1">
                <Skeleton className="h-3 w-24" />
                <Skeleton className="h-2.5 w-20" />
              </div>
              <div className="flex items-center gap-1.5">
                <Skeleton className="h-3.5 w-3.5" />
                <Skeleton className="h-3 w-20" />
                <Skeleton className="h-4 w-5 rounded" />
              </div>
              <Skeleton className="h-5 w-14 rounded-md" />
              <Skeleton className="h-3.5 w-16" />
              <Skeleton className="h-3.5 w-16" />
            </div>
        ))}
      </div>
  );
}

export function RecentSales({
                              sales,
                              loading = false,
                              pageSize = 8,
                              page,
                              totalCount,
                              totalPages,
                              onPageChange,
                            }: RecentSalesProps) {
  const pageData = sales;
  const startIndex = (page - 1) * pageSize + 1;
  const endIndex = Math.min(page * pageSize, totalCount);

  return (
      <Card className="shadow-sm">
        <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-4">
          <div>
            <CardTitle className="text-base font-semibold">Recent Sales</CardTitle>
            <CardDescription className="mt-1">
              Latest transactions across all managers
            </CardDescription>
          </div>
          {!loading && sales.length > 0 && (
              <Badge variant="outline" className="font-medium tabular-nums">
                {totalCount} total
              </Badge>
          )}
        </CardHeader>

        <CardContent className="px-0 pb-0">
          {loading ? (
              <SkeletonRows rows={pageSize} />
          ) : sales.length === 0 ? (
              <div className="flex py-16 items-center justify-center">
                <EmptyState
                    icon={<Receipt className="h-7 w-7 text-muted-foreground" />}
                    title="No sales in selected period"
                    description="Sales transactions will appear here once they are recorded for this date range."
                />
              </div>
          ) : (
              <>
                <Table>
                  <TableHeader>
                    <TableRow className="hover:bg-transparent">
                      <TableHead className="pl-6 w-[100px]">Date</TableHead>
                      <TableHead className="w-[140px]">Manager</TableHead>
                      <TableHead>Customer</TableHead>
                      <TableHead className="w-[180px]">Products</TableHead>
                      <TableHead className="w-[100px]">Status</TableHead>
                      <TableHead className="text-right">Amount</TableHead>
                      <TableHead className="pr-6 text-right">Gross Profit</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {pageData.map((sale) => {
                      const statusCfg = STATUS_CONFIG[sale.status];

                      // Вынесенный nested ternary
                      let grossProfitClass = 'text-foreground font-medium';
                      if (sale.grossProfit < 0) {
                        grossProfitClass = 'text-destructive font-medium';
                      } else if (sale.grossProfit === 0) {
                        grossProfitClass = 'text-muted-foreground';
                      }

                      return (
                          <TableRow
                              key={sale.id}
                              className={cn(
                                  'transition-colors hover:bg-muted/40',
                                  sale.status === 'Refunded' && 'bg-destructive/[0.03]',
                                  sale.status === 'Cancelled' && 'bg-muted/30',
                              )}
                          >
                            <TableCell className="pl-6 py-2.5 text-xs text-muted-foreground whitespace-nowrap">
                              {sale.date}
                            </TableCell>
                            <TableCell className="py-2.5">
                              <div className="flex items-center gap-2">
                                <Avatar className="h-7 w-7 border">
                                  <AvatarFallback className="bg-primary/10 text-[10px] font-semibold text-primary">
                                    {sale.managerInitials}
                                  </AvatarFallback>
                                </Avatar>
                                <span className="text-sm font-medium whitespace-nowrap">
                            {sale.managerName}
                          </span>
                              </div>
                            </TableCell>
                            <TableCell className="py-2.5">
                              <div className="flex flex-col leading-tight">
                                <span className="text-sm font-medium">{sale.customerName}</span>
                                <span className="text-xs text-muted-foreground">
                            {sale.customerCompany}
                          </span>
                              </div>
                            </TableCell>
                            <TableCell className="py-2.5">
                              <div className="flex items-center gap-1.5">
                                <Package className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                                <span className="text-xs text-muted-foreground truncate max-w-[140px]">
                            {sale.productSummary}
                          </span>
                                {sale.productCount > 1 && (
                                    <Badge
                                        variant="outline"
                                        className="shrink-0 h-4 px-1 text-[10px] font-medium"
                                    >
                                      {sale.productCount}
                                    </Badge>
                                )}
                              </div>
                            </TableCell>
                            <TableCell className="py-2.5">
                              <Badge variant={statusCfg.variant} className="gap-1.5">
                                <span className={cn('h-1.5 w-1.5 rounded-full', statusCfg.dot)} />
                                {statusCfg.label}
                              </Badge>
                            </TableCell>
                            <TableCell className="py-2.5 text-right text-sm font-semibold tabular-nums whitespace-nowrap">
                              {formatCurrency(sale.amount)}
                            </TableCell>
                            <TableCell className="pr-6 py-2.5 text-right text-sm tabular-nums whitespace-nowrap">
                        <span className={grossProfitClass}>
                          {formatCurrency(sale.grossProfit)}
                        </span>
                            </TableCell>
                          </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>

                {/* Pagination */}
                <div className="flex items-center justify-between px-6 py-3 border-t">
                  <p className="text-xs text-muted-foreground tabular-nums">
                    Showing {startIndex}–{endIndex} of {totalCount}
                  </p>
                  <div className="flex items-center gap-2">
                    <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        className="h-8 gap-1.5 px-2.5"
                        disabled={page <= 1}
                        onClick={(e) => {
                          e.preventDefault();
                          onPageChange(page - 1);
                        }}
                    >
                      <ChevronLeft className="h-3.5 w-3.5" />
                      Prev
                    </Button>
                    <span className="text-xs font-medium tabular-nums text-muted-foreground">
                  {page} / {totalPages}
                </span>
                    <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        className="h-8 gap-1.5 px-2.5"
                        disabled={page >= totalPages}
                        onClick={(e) => {
                          e.preventDefault();
                          onPageChange(page + 1);
                        }}
                    >
                      Next
                      <ChevronRight className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>
              </>
          )}
        </CardContent>
      </Card>
  );
}
