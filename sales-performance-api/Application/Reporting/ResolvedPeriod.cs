namespace sales_performance_api.Application.Reporting;

public sealed record ResolvedPeriod(
    PeriodKey Key,
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    int Days,
    DateTime StartUtc,
    DateTime EndExclusiveUtc,
    DateTime PreviousStartUtc,
    DateTime PreviousEndExclusiveUtc,
    string TimeZone);
