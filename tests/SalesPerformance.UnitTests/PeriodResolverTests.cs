using Microsoft.Extensions.Options;
using sales_performance_api.Application.Reporting;
using Xunit;

namespace SalesPerformance.UnitTests;

public sealed class PeriodResolverTests
{
	[Theory]
	[InlineData(PeriodKey.Today, "2026-09-23", "2026-09-23", "2026-09-22", "2026-09-22", 1)]
	[InlineData(PeriodKey.Last7, "2026-09-17", "2026-09-23", "2026-09-10", "2026-09-16", 7)]
	[InlineData(PeriodKey.Last30, "2026-08-25", "2026-09-23", "2026-07-26", "2026-08-24", 30)]
	[InlineData(PeriodKey.ThisMonth, "2026-09-01", "2026-09-23", "2026-08-09", "2026-08-31", 23)]
	[InlineData(PeriodKey.LastMonth, "2026-08-01", "2026-08-31", "2026-07-01", "2026-07-31", 31)]
	public void Presets_have_inclusive_equal_length_adjacent_periods(PeriodKey key, string from, string to,
		string previousFrom, string previousTo, int days)
	{
		var clock = new CountingClock("2026-09-23T12:00:00Z");
		var actual = Create(clock).Resolve(new PeriodRequest(key));
		Assert.Equal(DateOnly.Parse(from), actual.From);
		Assert.Equal(DateOnly.Parse(to), actual.To);
		Assert.Equal(DateOnly.Parse(previousFrom), actual.PreviousFrom);
		Assert.Equal(DateOnly.Parse(previousTo), actual.PreviousTo);
		Assert.Equal(days, actual.Days);
		Assert.Equal(days, actual.PreviousTo.DayNumber - actual.PreviousFrom.DayNumber + 1);
		Assert.Equal(actual.StartUtc, actual.PreviousEndExclusiveUtc);
		Assert.Equal(DateTimeKind.Utc, actual.StartUtc.Kind);
		Assert.Equal(1, clock.Reads);
	}

	[Theory]
	[InlineData("2026-01-15T12:00:00Z", PeriodKey.LastMonth, "2025-12-01", "2025-12-31", "2025-10-31", "2025-11-30")]
	[InlineData("2024-03-01T12:00:00Z", PeriodKey.LastMonth, "2024-02-01", "2024-02-29", "2024-01-03", "2024-01-31")]
	[InlineData("2026-09-01T12:00:00Z", PeriodKey.ThisMonth, "2026-09-01", "2026-09-01", "2026-08-31", "2026-08-31")]
	public void Month_boundaries_are_calendar_based(string now, PeriodKey key, string from, string to,
		string previousFrom, string previousTo)
	{
		var actual = Create(new CountingClock(now)).Resolve(new PeriodRequest(key));
		Assert.Equal(DateOnly.Parse(from), actual.From);
		Assert.Equal(DateOnly.Parse(to), actual.To);
		Assert.Equal(DateOnly.Parse(previousFrom), actual.PreviousFrom);
		Assert.Equal(DateOnly.Parse(previousTo), actual.PreviousTo);
	}

	[Theory]
	[InlineData("2026-03-29", "2026-03-28T23:00:00Z", "2026-03-29T22:00:00Z", 23)]
	[InlineData("2026-10-25", "2026-10-24T22:00:00Z", "2026-10-25T23:00:00Z", 25)]
	public void Dst_days_have_correct_utc_bounds(string day, string start, string end, int hours)
	{
		var date = DateOnly.Parse(day);
		var actual = Create(new CountingClock("2026-11-01T12:00:00Z"))
			.Resolve(new PeriodRequest(PeriodKey.Custom, date, date));
		Assert.Equal(DateTimeOffset.Parse(start).UtcDateTime, actual.StartUtc);
		Assert.Equal(DateTimeOffset.Parse(end).UtcDateTime, actual.EndExclusiveUtc);
		Assert.Equal(hours, (actual.EndExclusiveUtc - actual.StartUtc).TotalHours);
		Assert.Equal(1, actual.Days);
	}

	[Fact]
	public void Today_uses_reporting_timezone_instead_of_utc_or_machine_timezone()
	{
		var actual = Create(new CountingClock("2026-09-23T22:30:00Z")).Resolve(new PeriodRequest(PeriodKey.Today));
		Assert.Equal(new DateOnly(2026, 9, 24), actual.From);
		Assert.Equal(DateTimeOffset.Parse("2026-09-23T22:00:00Z").UtcDateTime, actual.StartUtc);
	}

	[Theory]
	[InlineData("2026-03-08", "2026-03-08T05:00:00Z")]
	[InlineData("2026-11-01", "2026-11-01T04:00:00Z")]
	public void Midnight_transitions_choose_first_existing_instant(string date, string start)
	{
		var day = DateOnly.Parse(date);
		var actual = Create(new CountingClock("2026-11-02T12:00:00Z"), "America/Havana")
			.Resolve(new PeriodRequest(PeriodKey.Custom, day, day));
		Assert.Equal(DateTimeOffset.Parse(start).UtcDateTime, actual.StartUtc);
	}

	[Theory]
	[InlineData(PeriodKey.Custom, null, "2026-09-23", "from")]
	[InlineData(PeriodKey.Custom, "2026-09-23", null, "to")]
	[InlineData(PeriodKey.Custom, "2026-09-23", "2026-09-22", "to")]
	[InlineData(PeriodKey.Custom, "2026-09-23", "2026-09-24", "to")]
	[InlineData(PeriodKey.Custom, "1899-12-31", "1900-01-01", "from")]
	[InlineData(PeriodKey.Custom, "2025-09-22", "2026-09-23", "to")]
	[InlineData(PeriodKey.Last7, "2026-09-01", null, "from")]
	[InlineData(PeriodKey.Last30, null, "2026-09-23", "to")]
	[InlineData((PeriodKey)99, null, null, "period")]
	public void Invalid_requests_are_rejected_with_field_names(PeriodKey key, string? from, string? to, string field)
	{
		var resolver = Create(new CountingClock("2026-09-23T12:00:00Z"));
		var error = Assert.Throws<PeriodValidationException>(() => resolver.Resolve(new PeriodRequest(key,
			from is null ? null : DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to))));
		Assert.Equal(field, error.Field);
	}

	[Fact]
	public void Full_366_day_range_is_allowed_and_default_is_last30()
	{
		var resolver = Create(new CountingClock("2026-09-23T12:00:00Z"));
		Assert.Equal(PeriodKey.Last30, resolver.Resolve(new PeriodRequest()).Key);
		Assert.Equal(366, resolver.Resolve(new PeriodRequest(PeriodKey.Custom,
			new DateOnly(2025, 9, 23), new DateOnly(2026, 9, 23))).Days);
		var earliest = resolver.Resolve(new PeriodRequest(PeriodKey.Custom,
			new DateOnly(1900, 1, 1), new DateOnly(1900, 1, 1)));
		Assert.Equal(new DateOnly(1899, 12, 31), earliest.PreviousFrom);
	}

	[Theory]
	[InlineData("")]
	[InlineData("Unknown/Zone")]
	[InlineData("Central Europe Standard Time")]
	public void Reporting_zone_must_be_valid_for_dotnet_and_postgres(string zone)
	{
		Assert.False(ReportingOptions.IsValidTimeZone(zone));
		Assert.Throws<ArgumentException>(() => Create(new CountingClock("2026-09-23T12:00:00Z"), zone));
	}

	private static PeriodResolver Create(TimeProvider clock, string zone = "Europe/Belgrade") =>
		new(clock, Options.Create(new ReportingOptions { TimeZone = zone }));

	private sealed class CountingClock(string now) : TimeProvider
	{
		public int Reads { get; private set; }

		public override DateTimeOffset GetUtcNow()
		{
			Reads++;
			return DateTimeOffset.Parse(now);
		}
	}
}
