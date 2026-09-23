using System.Globalization;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Application.Reporting;

namespace sales_performance_api.Api;

// Read original query values explicitly: reject duplicates, numeric enums and unknown keys.
public sealed class AnalyticsQuery(IQueryCollection query, params string[] additionalKeys)
{
	public PeriodRequest Period()
	{
		var allowed =
			new HashSet<string>(["preset", "period", "from", "to", .. additionalKeys], StringComparer.Ordinal);
		foreach (var (key, values) in query)
		{
			if (!allowed.Contains(key)) Fail(key, "Unknown query parameter.");
			if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
				Fail(key, "Supply exactly one non-empty value.");
		}

		var preset = Value("preset");
		var period = Value("period");
		if (preset is not null && period is not null) Fail("preset", "Use either preset or period, not both.");
		var name = preset ?? period;
		var from = Date("from");
		var to = Date("to");
		var resolvedKey = name?.ToLowerInvariant() switch
		{
			null => from.HasValue || to.HasValue ? PeriodKey.Custom : PeriodKey.Last30,
			"today" => PeriodKey.Today, "last7" => PeriodKey.Last7, "last30" => PeriodKey.Last30,
			"thismonth" => PeriodKey.ThisMonth, "lastmonth" => PeriodKey.LastMonth, "custom" => PeriodKey.Custom,
			_ => throw new PeriodValidationException(preset is null ? "period" : "preset",
				"Use today, last7, last30, thisMonth, lastMonth or custom.")
		};
		return new(resolvedKey, from, to);
	}

	public RankingSort Sort() => Value("sortBy")?.ToLowerInvariant() switch
	{
		null or "grossprofit" => RankingSort.GrossProfit,
		"averagecheck" or "avgcheck" => RankingSort.AverageCheck,
		_ => throw new PeriodValidationException("sortBy", "Use GrossProfit or AverageCheck.")
	};

	public int Integer(string name, int fallback, int minimum, int maximum)
	{
		var text = Value(name);
		if (text is null) return fallback;
		if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < minimum ||
		    value > maximum)
			Fail(name, $"Must be an integer between {minimum} and {maximum}.");
		return value;
	}

	private string? Value(string name) => query.TryGetValue(name, out var value) ? value.ToString() : null;

	private DateOnly? Date(string name)
	{
		var text = Value(name);
		if (text is null) return null;
		if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
			    out var value))
			Fail(name, "Use a valid date in YYYY-MM-DD format.");
		return value;
	}

	private static void Fail(string field, string message) => throw new PeriodValidationException(field, message);
}
