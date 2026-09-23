using System.Data;
using Microsoft.EntityFrameworkCore;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Application.Reporting;
using sales_performance_api.Infrastructure.Persistence;

namespace sales_performance_api.Infrastructure.Queries;

public sealed class PostgresAnalyticsDetailsReader(SalesDbContext db) : IAnalyticsDetailsReader
{
	public async Task<IReadOnlyList<CategoryViewModel>> CategoriesAsync(ResolvedPeriod p, CancellationToken ct) =>
		await db.Database.SqlQuery<CategoryViewModel>($"""
		                                               WITH totals AS (
		                                                   SELECT c.id, c.name, c.sort_order, SUM(i.quantity * i.unit_sale_price) revenue,
		                                                          SUM(i.quantity * (i.unit_sale_price - i.unit_cost)) profit
		                                                   FROM public.sale_items i JOIN public.sales s ON s.id = i.sale_id
		                                                   JOIN public.categories c ON c.id = i.category_id_at_sale
		                                                   WHERE s.status = 'Paid' AND s.sale_date >= {p.StartUtc} AND s.sale_date < {p.EndExclusiveUtc}
		                                                   GROUP BY c.id, c.name, c.sort_order
		                                               )
		                                               SELECT id AS "Id", name AS "Name", revenue AS "Revenue", profit AS "GrossProfit",
		                                                      COALESCE(ROUND(revenue / NULLIF(SUM(revenue) OVER (), 0) * 100, 2), 0) AS "RevenueShare",
		                                                      CASE WHEN SUM(profit) OVER () < 0 THEN NULL
		                                                           ELSE COALESCE(ROUND(profit / NULLIF(SUM(profit) OVER (), 0) * 100, 2), 0) END AS "GrossProfitShare"
		                                               FROM totals ORDER BY sort_order, id
		                                               """).ToListAsync(ct);

	public async Task<IReadOnlyList<ProductViewModel>>
		ProductsAsync(ResolvedPeriod p, int limit, CancellationToken ct) =>
		await db.Database.SqlQuery<ProductViewModel>($"""
		                                              SELECT i.product_id::text || ':' || i.category_id_at_sale::text AS "Id",
		                                                     i.product_id AS "ProductId", i.category_id_at_sale AS "CategoryId",
		                                                     (array_agg(i.product_name_at_sale ORDER BY s.sale_date DESC, s.id DESC, i.line_number DESC))[1] AS "Name",
		                                                     c.name AS "Category", SUM(i.quantity * i.unit_sale_price) AS "Revenue",
		                                                     SUM(i.quantity * (i.unit_sale_price - i.unit_cost)) AS "GrossProfit",
		                                                     COALESCE(ROUND(SUM(i.quantity * (i.unit_sale_price - i.unit_cost)) /
		                                                         NULLIF(SUM(i.quantity * i.unit_sale_price), 0) * 100, 2), 0) AS "Margin"
		                                              FROM public.sale_items i JOIN public.sales s ON s.id = i.sale_id
		                                              JOIN public.categories c ON c.id = i.category_id_at_sale
		                                              WHERE s.status = 'Paid' AND s.sale_date >= {p.StartUtc} AND s.sale_date < {p.EndExclusiveUtc}
		                                              GROUP BY i.product_id, i.category_id_at_sale, c.name
		                                              ORDER BY "Revenue" DESC, "GrossProfit" DESC, i.product_id, i.category_id_at_sale
		                                              LIMIT {limit}
		                                              """).ToListAsync(ct);

	public async Task<Page<RecentSaleViewModel>> RecentAsync(ResolvedPeriod p, int page, int pageSize,
		CancellationToken ct)
	{
		// Count and page must see the same snapshot, even if sales change between statements.
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
		var total = await db.Sales.LongCountAsync(s => s.SaleDate >= p.StartUtc && s.SaleDate < p.EndExclusiveUtc, ct);
		var offset = ((long)page - 1) * pageSize;
		var rows = await db.Database.SqlQuery<RecentSaleViewModel>($"""
		                                                            WITH selected AS (
		                                                                SELECT * FROM public.sales
		                                                                WHERE sale_date >= {p.StartUtc} AND sale_date < {p.EndExclusiveUtc}
		                                                                ORDER BY sale_date DESC, id DESC LIMIT {pageSize} OFFSET {offset}
		                                                            )
		                                                            SELECT s.id AS "Id", s.sale_date AS "Date", m.name AS "ManagerName", m.initials AS "ManagerInitials",
		                                                                   c.name AS "CustomerName", c.company AS "CustomerCompany", s.status AS "Status",
		                                                                   COALESCE(string_agg(i.product_name_at_sale, ', ' ORDER BY i.line_number), '') AS "ProductSummary",
		                                                                   COUNT(i.id)::integer AS "ProductCount", COALESCE(SUM(i.quantity * i.unit_sale_price), 0) AS "Amount",
		                                                                   CASE WHEN s.status = 'Paid' THEN COALESCE(SUM(i.quantity * (i.unit_sale_price - i.unit_cost)), 0)
		                                                                        ELSE 0 END AS "GrossProfit"
		                                                            FROM selected s JOIN public.managers m ON m.id = s.manager_id
		                                                            JOIN public.customers c ON c.id = s.customer_id LEFT JOIN public.sale_items i ON i.sale_id = s.id
		                                                            GROUP BY s.id, s.sale_date, m.name, m.initials, c.name, c.company, s.status
		                                                            ORDER BY s.sale_date DESC, s.id DESC
		                                                            """).ToListAsync(ct);
		await transaction.CommitAsync(ct);
		return new(rows, page, pageSize, total, (total + pageSize - 1) / pageSize);
	}
}
