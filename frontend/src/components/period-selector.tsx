import { useState } from 'react';
import { format } from 'date-fns';
import { Calendar as CalendarIcon, ChevronDown } from 'lucide-react';
import { type DateRange as ReactDayPickerRange } from 'react-day-picker';

import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { Separator } from '@/components/ui/separator';
import {
  PERIOD_PRESETS,
  type PeriodKey,
  type DateRange,
  formatRange,
} from '@/lib/analytics-api';

interface PeriodSelectorProps {
  period: PeriodKey;
  onPeriodChange: (key: PeriodKey) => void;
  customRange: DateRange | null;
  onCustomRangeChange: (range: DateRange) => void;
}

export function PeriodSelector({
  period,
  onPeriodChange,
  customRange,
  onCustomRangeChange,
}: PeriodSelectorProps) {
  const [open, setOpen] = useState(false);
  const [calendarRange, setCalendarRange] = useState<
    ReactDayPickerRange | undefined
  >(
    customRange
      ? { from: customRange.from, to: customRange.to }
      : undefined,
  );


  function handlePresetClick(key: PeriodKey) {
    if (key === 'custom') return;
    onPeriodChange(key);
  }

  function handleCalendarSelect(range: ReactDayPickerRange | undefined) {
    setCalendarRange(range);
    if (range?.from && range?.to) {
      onCustomRangeChange({ from: range.from, to: range.to });
      onPeriodChange('custom');
      setOpen(false);
    }
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          className="h-10 min-w-[260px] justify-between gap-3 border-border bg-card px-4 font-medium shadow-sm hover:bg-accent"
        >
          <div className="flex items-center gap-2.5">
            <CalendarIcon className="h-4 w-4 text-primary" />
            <span className="text-sm">
              {period === 'custom' && customRange
                ? formatRange(customRange)
                : PERIOD_PRESETS.find((p) => p.key === period)?.label}
            </span>
          </div>
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align="end"
        className="w-[420px] p-0"
        sideOffset={8}
      >
        <div className="flex">
          <div className="w-[150px] shrink-0 border-r bg-muted/30 p-2">
            <p className="px-2 py-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Presets
            </p>
            <div className="flex flex-col gap-0.5">
              {PERIOD_PRESETS.map((preset) => (
                <button
                  key={preset.key}
                  onClick={() => handlePresetClick(preset.key)}
                  className={cn(
                    'rounded-md px-2.5 py-2 text-left text-sm font-medium transition-colors',
                    period === preset.key
                      ? 'bg-primary text-primary-foreground shadow-sm'
                      : 'text-foreground hover:bg-accent',
                  )}
                >
                  {preset.label}
                </button>
              ))}
            </div>
          </div>
          <div className="flex-1 p-3">
            <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Custom Range
            </p>
            <Calendar
              mode="range"
              selected={calendarRange}
              onSelect={handleCalendarSelect}
              numberOfMonths={1}
              disabled={[{ after: new Date() }]}
            />
            <Separator className="my-3" />
            <div className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground">Selected range</span>
              <span className="font-medium text-foreground">
                {calendarRange?.from
                  ? format(calendarRange.from, 'MMM d, yyyy')
                  : '—'}
                {' → '}
                {calendarRange?.to
                  ? format(calendarRange.to, 'MMM d, yyyy')
                  : '—'}
              </span>
            </div>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  );
}
