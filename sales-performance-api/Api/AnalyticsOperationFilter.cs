using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace sales_performance_api.Api;

public sealed class AnalyticsOperationFilter : IOperationFilter
{
	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		var route = context.ApiDescription.RelativePath ?? "";
		if (!route.StartsWith("api/analytics/", StringComparison.Ordinal)) return;
		operation.Parameters ??= new List<OpenApiParameter>();
		foreach (var status in new[] { 400, 404, 405, 500, 503 })
		{
			var type = status == 400
				? typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails)
				: typeof(Microsoft.AspNetCore.Mvc.ProblemDetails);
			operation.Responses[status.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
				new OpenApiResponse
				{
					Description = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
					Content = new Dictionary<string, OpenApiMediaType>
					{
						["application/problem+json"] = new()
							{ Schema = context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository) }
					}
				};
		}

		void Add(string name, string description, string type = "string", string? format = null,
			int? defaultValue = null, int? min = null, int? max = null)
		{
			operation.Parameters.Add(new OpenApiParameter
			{
				Name = name, In = ParameterLocation.Query, Description = description,
				Schema = new OpenApiSchema
				{
					Type = type, Format = format, Minimum = min, Maximum = max,
					Default = defaultValue.HasValue ? new OpenApiInteger(defaultValue.Value) : null
				}
			});
		}

		Add("preset",
			"today | last7 | last30 | thisMonth | lastMonth | custom. Default last30. Mutually exclusive with period; dates only with custom.");
		Add("period", "Alias for preset.");
		Add("from",
			"Inclusive date; requires to. Without preset implies custom. Earliest 1900-01-01; maximum range 366 days.",
			format: "date");
		Add("to", "Inclusive date, not after today in reporting timezone.", format: "date");
		if (route.EndsWith("ranking"))
			Add("sortBy", "GrossProfit (default) | AverageCheck. Aliases grossProfit, avgCheck accepted.");
		if (route.EndsWith("top")) Add("limit", "Maximum number of products.", "integer", "int32", 8, 1, 100);
		if (route.EndsWith("recent"))
		{
			Add("page", "One-based page.", "integer", "int32", 1, 1, int.MaxValue);
			Add("pageSize", "Items per page.", "integer", "int32", 8, 1, 100);
		}
	}
}
