using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using sales_performance_api.Application.Reporting;

namespace sales_performance_api.Api;

public static class ApiErrors
{
	public static async Task Invoke(HttpContext context, RequestDelegate next, ILogger logger)
	{
		try
		{
			await next(context);
		}
		catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
		{
			context.Abort();
		}
		catch (PeriodValidationException ex)
		{
			await Write(context, 400, "validation_error", "Invalid query parameters.",
				new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
		}
		catch (Exception ex) when (!context.Response.HasStarted)
		{
			// Do not expose SQL, parameters, connection strings or stack traces in responses/log messages.
			logger.LogError("Analytics request failed ({ExceptionType}), trace {TraceId}", ex.GetType().Name,
				context.TraceIdentifier);
			var unavailable = ex is NpgsqlException or TimeoutException;
			await Write(context, unavailable ? 503 : 500, unavailable ? "database_unavailable" : "internal_error",
				unavailable
					? "The database is temporarily unavailable. Please retry."
					: "An unexpected error occurred.");
		}
	}

	public static Task Write(HttpContext context, int status, string code, string detail,
		Dictionary<string, string[]>? errors = null)
	{
		ProblemDetails problem = errors is null ? new ProblemDetails() : new ValidationProblemDetails(errors);
		problem.Type = "about:blank";
		problem.Status = status;
		problem.Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status);
		problem.Detail = detail;
		problem.Instance = context.Request.Path;
		problem.Extensions["code"] = code;
		problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
		context.Response.StatusCode = status;
		return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
			contentType: "application/problem+json", cancellationToken: context.RequestAborted);
	}
}
