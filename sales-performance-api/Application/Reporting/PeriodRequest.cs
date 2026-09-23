namespace sales_performance_api.Application.Reporting;

public sealed record PeriodRequest(PeriodKey Period = PeriodKey.Last30, DateOnly? From = null, DateOnly? To = null);
