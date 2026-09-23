using Microsoft.EntityFrameworkCore;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Infrastructure.Persistence;

namespace sales_performance_api.Infrastructure.Queries;

public sealed class PostgresAnalyticsReader(SalesDbContext db) : IAnalyticsReader
{
	public async Task<IReadOnlyList<ManagerDailyAggregate>> ReadAsync(DateTime startUtc, DateTime endExclusiveUtc,
		string timeZone, CancellationToken cancellationToken)
	{
		// One statement = one snapshot for current/previous/leader/sparklines.
		// First aggregate each cheque so multiple items never multiply SalesCount.
		// SqlQuery parameterizes every interpolated value (including the IANA zone).
		return await db.Database.SqlQuery<ManagerDailyAggregate>($"""
		                                                          WITH per_sale AS (
		                                                              SELECT s.id, s.manager_id, (s.sale_date AT TIME ZONE {timeZone})::date AS local_date,
		                                                                     COALESCE(SUM(i.quantity * i.unit_sale_price), 0) AS revenue,
		                                                                     COALESCE(SUM(i.quantity * i.unit_cost), 0) AS cost
		                                                              FROM public.sales AS s
		                                                              LEFT JOIN public.sale_items AS i ON i.sale_id = s.id
		                                                              WHERE s.status = 'Paid' AND s.sale_date >= {startUtc} AND s.sale_date < {endExclusiveUtc}
		                                                              GROUP BY s.id, s.manager_id, s.sale_date
		                                                          ), daily AS (
		                                                              SELECT manager_id, local_date, SUM(revenue) AS revenue, SUM(cost) AS cost,
		                                                                     COUNT(*)::integer AS sales_count
		                                                              FROM per_sale
		                                                              GROUP BY manager_id, local_date
		                                                          )
		                                                          SELECT d.manager_id AS "ManagerId", m.name AS "ManagerName", m.initials AS "ManagerInitials", d.local_date AS "Date",
		                                                                 d.revenue AS "Revenue", d.cost AS "Cost", d.sales_count AS "SalesCount"
		                                                          FROM daily AS d
		                                                          JOIN public.managers AS m ON m.id = d.manager_id
		                                                          """).ToListAsync(cancellationToken);
	}
}
