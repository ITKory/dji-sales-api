import {
  addDays,
  format,
} from 'date-fns';

export interface DateRange {
  from: Date;
  to: Date;
}

export function daysInRange(range: DateRange): number {
  return Math.round((range.to.getTime() - range.from.getTime()) / 86_400_000) + 1;
}

export function generateDailyData(range: DateRange) {
  const days = daysInRange(range);
  const points: { date: string; revenue: number; deals: number; target: number }[] = [];
  const base = 42000;
  for (let i = 0; i < days; i++) {
    const date = addDays(range.from, i);
    const weekday = date.getDay();
    const weekendDip = weekday === 0 || weekday === 6 ? 0.55 : 1;
    const trend = 1 + i * 0.008;
    const noise = 0.82 + Math.sin(i * 0.9) * 0.12 + Math.cos(i * 0.4) * 0.06;
    const revenue = Math.round(base * weekendDip * trend * noise);
    const deals = Math.round((revenue / 5200) * (0.9 + Math.random() * 0.2));
    const target = Math.round(base * weekendDip * trend * 0.95);
    points.push({
      date: format(date, days > 60 ? 'MMM d' : 'EEE'),
      revenue,
      deals,
      target,
    });
  }
  return points;
}

export interface SalesRep {
  name: string;
  initials: string;
  revenue: number;
  deals: number;
  quota: number;
  trend: number;
}
export interface Deal {
  company: string;
  logo: string;
  rep: string;
  value: number;
  stage: 'Closed Won' | 'Negotiation' | 'Proposal Sent' | 'Discovery';
  closeDate: string;
}
export interface FunnelStage {
  stage: string;
  count: number;
  value: number;
  conversion: number;
}
export function getCategoryBreakdown() {
  return [
    { name: 'Enterprise', value: 542_000, fill: 'hsl(var(--chart-1))' },
    { name: 'Mid-Market', value: 386_000, fill: 'hsl(var(--chart-2))' },
    { name: 'SMB', value: 214_000, fill: 'hsl(var(--chart-3))' },
    { name: 'Startup', value: 142_000, fill: 'hsl(var(--chart-4))' },
  ];
}
