namespace sales_performance_api.Application.Reporting;

public sealed class ReportingOptions
{
	public const string SectionName = "Reporting";
	public string TimeZone { get; set; } = "Europe/Belgrade";

	public static bool IsValidTimeZone(string? id) => !string.IsNullOrWhiteSpace(id)
	                                                  && (id == "UTC" ||
	                                                      TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _))
	                                                  && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
