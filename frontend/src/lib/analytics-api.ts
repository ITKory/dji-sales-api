import { format } from 'date-fns';

export type PeriodKey = 'today' | 'last7' | 'last30' | 'thisMonth' | 'lastMonth' | 'custom';
export interface DateRange { from: Date; to: Date }
export const PERIOD_PRESETS: { key: PeriodKey; label: string }[] = [
  { key: 'today', label: 'Today' }, { key: 'last7', label: 'Last 7 days' },
  { key: 'last30', label: 'Last 30 days' }, { key: 'thisMonth', label: 'This month' },
  { key: 'lastMonth', label: 'Last month' }, { key: 'custom', label: 'Custom range' },
];
export const formatRange = (range: DateRange) => `${format(range.from, 'MMM d, yyyy')} → ${format(range.to, 'MMM d, yyyy')}`;
export interface ReportPeriod { key: PeriodKey; from: string; to: string; previousFrom: string; previousTo: string; days: number; timeZone: string }
export interface Report<T> { period: ReportPeriod; currency: string; data: T }
export interface KpiData {
  revenue: number; revenuePrev: number; revenueDelta: number | null;
  grossProfit: number; grossProfitPrev: number; grossProfitDelta: number | null;
  margin: number; marginPrev: number; marginDelta: number;
  salesCount: number; salesCountPrev: number; salesCountDelta: number | null;
  avgCheck: number; avgCheckPrev: number; avgCheckDelta: number | null;
  bestManagerId: string | null; bestManager: string | null; bestManagerValue: number; bestManagerPrev: number; bestManagerDelta: number | null;
  sparklines: Record<string, number[]>;
}
export type RankingMode = 'grossProfit' | 'avgCheck';
export type TimeSeriesMetric = 'revenue' | 'grossProfit' | 'salesCount';
export interface TimeSeriesPoint { date: string; revenue: number; grossProfit: number; salesCount: number }
export interface ManagerRanking {
  id: string; name: string; initials: string; salesCount: number; revenue: number; grossProfit: number;
  avgCheck: number; margin: number; grossProfitChange: number | null; avgCheckChange: number | null;
  timeSeries: TimeSeriesPoint[];
}
export interface CategoryProfit { id: string; name: string; revenue: number; grossProfit: number; revenueShare: number; grossProfitShare: number | null; fill: string }
export interface TopProduct { id: string; productId: string; categoryId: string; name: string; category: string; revenue: number; grossProfit: number; margin: number }
export type SaleStatus = 'Paid' | 'Cancelled' | 'Refunded';
export interface RecentSale { id: string; date: string; managerName: string; managerInitials: string; customerName: string; customerCompany: string; productSummary: string; productCount: number; status: SaleStatus; amount: number; grossProfit: number }
export interface SalesPage { items: RecentSale[]; pageNumber: number; pageSize: number; totalCount: number; totalPages: number }
export function periodQuery(period: PeriodKey, range: DateRange | null): string {
  if (period === 'custom' && range) return new URLSearchParams({ from: format(range.from, 'yyyy-MM-dd'), to: format(range.to, 'yyyy-MM-dd') }).toString();
  return new URLSearchParams({ preset: period }).toString();
}
export async function getReport<T>(endpoint: string, query: string, signal: AbortSignal): Promise<Report<T>> {
  const response = await fetch(`/api/analytics/${endpoint}?${query}`, { signal, headers: { Accept: 'application/json' } });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const errors = problem?.errors as Record<string, string[]> | undefined;
    throw new Error(errors ? Object.entries(errors).map(([key, messages]) => `${key}: ${messages.join(' ')}`).join(' · ')
      : problem?.detail ?? `Request failed (${response.status}).`);
  }
  return response.json();
}
