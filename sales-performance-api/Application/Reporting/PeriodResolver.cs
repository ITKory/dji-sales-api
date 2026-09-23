using Microsoft.Extensions.Options;

namespace sales_performance_api.Application.Reporting;

public sealed class PeriodResolver : IPeriodResolver
{
	private const int MaximumDays = 366;
	private static readonly DateOnly MinimumDate = new(1900, 1, 1);
	private readonly TimeProvider _clock;
	private readonly TimeZoneInfo _timeZone;

	public PeriodResolver(TimeProvider clock, IOptions<ReportingOptions> options)
	{
		_clock = clock;
		if (!ReportingOptions.IsValidTimeZone(options.Value.TimeZone))
			throw new ArgumentException("Reporting:TimeZone must be a valid IANA time zone or UTC.", nameof(options));
		_timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
	}

	public ResolvedPeriod Resolve(PeriodRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		// Capture time once: all presets and validation use the same reporting day.
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _timeZone).DateTime);
		if (!Enum.IsDefined(request.Period))
			throw new PeriodValidationException("period", "Unknown reporting period.");
		if (request.Period != PeriodKey.Custom && (request.From.HasValue || request.To.HasValue))
			throw new PeriodValidationException(request.From.HasValue ? "from" : "to",
				"Dates are only allowed for the custom period.");

		var monthStart = new DateOnly(today.Year, today.Month, 1);
		var (from, to) = request.Period switch
		{
			PeriodKey.Today => (today, today),
			PeriodKey.Last7 => (today.AddDays(-6), today),
			PeriodKey.Last30 => (today.AddDays(-29), today),
			PeriodKey.ThisMonth => (monthStart, today),
			PeriodKey.LastMonth => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
			_ => (request.From ?? throw new PeriodValidationException("from", "Custom period requires from."),
				request.To ?? throw new PeriodValidationException("to", "Custom period requires to."))
		};
		if (from < MinimumDate)
			throw new PeriodValidationException("from", "Must be on or after 1900-01-01.");
		if (to < from)
			throw new PeriodValidationException("to", "Must be on or after from.");
		if (to > today)
			throw new PeriodValidationException("to", "Must not be after today in the reporting time zone.");
		if (to == DateOnly.MaxValue)
			throw new PeriodValidationException("to", "The day after to must be representable.");
		var days = to.DayNumber - from.DayNumber + 1;
		if (days > MaximumDays)
			throw new PeriodValidationException("to", "Reporting period must not exceed 366 calendar days.");

		var previousFrom = from.AddDays(-days);
		var startUtc = StartOfDayUtc(from);
		return new ResolvedPeriod(request.Period, from, to, previousFrom, from.AddDays(-1), days,
			startUtc, StartOfDayUtc(to.AddDays(1)), StartOfDayUtc(previousFrom), startUtc, _timeZone.Id);
	}

	private DateTime StartOfDayUtc(DateOnly date)
	{
		var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
		// Some IANA zones advance clocks at midnight. Begin at the first existing local minute.
		while (_timeZone.IsInvalidTime(local)) local = local.AddMinutes(1);
		return _timeZone.IsAmbiguousTime(local)
			? new DateTimeOffset(local, _timeZone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime
			: TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
	}
}
