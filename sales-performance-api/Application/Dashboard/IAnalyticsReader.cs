namespace sales_performance_api.Application.Dashboard;

public interface IAnalyticsReader
{
	// Return only daily manager aggregates of Paid sales, in one consistent database snapshot.
	Task<IReadOnlyList<ManagerDailyAggregate>> ReadAsync(DateTime startUtc, DateTime endExclusiveUtc,
		string timeZone, CancellationToken cancellationToken);
}

public sealed record ManagerDailyAggregate(Guid ManagerId, string ManagerName, DateOnly Date, decimal Revenue, decimal Cost, int SalesCount, string ManagerInitials = "");
