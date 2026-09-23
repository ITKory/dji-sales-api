namespace sales_performance_api.Application.Reporting;

public interface IPeriodResolver
{
	ResolvedPeriod Resolve(PeriodRequest request);
}
