using sales_performance_api.Application.Reporting;

namespace sales_performance_api.Application.Dashboard;

public interface IDashboardQueryService
{
	Task<Report<KpisResponse>> GetKpisAsync(PeriodRequest request, CancellationToken cancellationToken = default);

	Task<Report<IReadOnlyList<TimeSeriesPointDto>>> GetDynamicsAsync(PeriodRequest request,
		CancellationToken cancellationToken = default);
}
