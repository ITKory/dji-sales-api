namespace sales_performance_api.Application.Dashboard;

public sealed record KpisResponse(
    decimal Revenue, decimal RevenuePrev, decimal? RevenueDelta,
    decimal GrossProfit, decimal GrossProfitPrev, decimal? GrossProfitDelta,
    decimal Margin, decimal MarginPrev, decimal MarginDelta,
    int SalesCount, int SalesCountPrev, decimal? SalesCountDelta,
    decimal AvgCheck, decimal AvgCheckPrev, decimal? AvgCheckDelta,
    string? BestManagerId, string? BestManager, decimal BestManagerValue, decimal BestManagerPrev, decimal? BestManagerDelta,
    IReadOnlyDictionary<string, IReadOnlyList<decimal>> Sparklines);

public sealed record TimeSeriesPointDto(string Date, decimal Revenue, decimal GrossProfit, int SalesCount);
