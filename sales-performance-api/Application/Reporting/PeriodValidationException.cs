namespace sales_performance_api.Application.Reporting;

// The HTTP boundary can map Field/Message to ValidationProblemDetails without coupling Application to MVC.
public sealed class PeriodValidationException(string field, string message) : ArgumentException(message)
{
	public string Field { get; } = field;
}
