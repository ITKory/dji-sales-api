namespace sales_performance_api.Application.Reporting;

public sealed record Report<T>(ReportPeriod Period, string Currency, T Data);

// Public report metadata excludes persistence-specific UTC bounds.
public sealed record ReportPeriod(
	PeriodKey Key,
	DateOnly From,
	DateOnly To,
	DateOnly PreviousFrom,
	DateOnly PreviousTo,
	int Days,
	string TimeZone)
{
	public static ReportPeriod FromResolved(ResolvedPeriod period) => new(period.Key, period.From, period.To,
		period.PreviousFrom, period.PreviousTo, period.Days, period.TimeZone);
}
