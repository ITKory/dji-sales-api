using sales_performance_api.Api;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using sales_performance_api.Application.Dashboard;
using sales_performance_api.Application.Reporting;
using sales_performance_api.Infrastructure.Queries;
using sales_performance_api.Infrastructure.Persistence;
using sales_performance_api.Infrastructure.Persistence.Seed;

var initializeDatabase = args.Contains("--initialize-database", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--initialize-database").ToArray());

var connectionString = builder.Configuration.GetConnectionString("SalesDatabase");
if (string.IsNullOrWhiteSpace(connectionString) && !EF.IsDesignTime)
    throw new InvalidOperationException("Set ConnectionStrings__SalesDatabase before starting the application.");
builder.Services.AddDbContext<SalesDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<ReportingOptions>()
    .Bind(builder.Configuration.GetSection(ReportingOptions.SectionName))
    .Validate(options => ReportingOptions.IsValidTimeZone(options.TimeZone), "Reporting:TimeZone must be an IANA time zone or UTC.")
    .ValidateOnStart();
builder.Services.AddSingleton<IPeriodResolver, PeriodResolver>();
builder.Services.AddScoped<IAnalyticsReader, PostgresAnalyticsReader>();
builder.Services.AddScoped<IDashboardQueryService, DashboardQueryService>();
builder.Services.AddScoped<IAnalyticsDetailsReader, PostgresAnalyticsDetailsReader>();
builder.Services.AddScoped<AnalyticsDetailsService>();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<PeriodKey>(JsonNamingPolicy.CamelCase, false)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.OperationFilter<AnalyticsOperationFilter>());

await using var app = builder.Build();

if (initializeDatabase)
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(
        builder.Configuration.GetValue<bool>("Seed:Enabled"), cancellation.Token);
    return;
}

app.Use((context, next) => ApiErrors.Invoke(context, next, app.Logger));
app.UseStatusCodePages(context => ApiErrors.Write(context.HttpContext, context.HttpContext.Response.StatusCode,
    context.HttpContext.Response.StatusCode == 405 ? "method_not_allowed" : "route_not_found",
    context.HttpContext.Response.StatusCode == 405 ? "This HTTP method is not supported for the route." : "The requested route was not found."));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Compose uses internal HTTP; TLS termination is a separate deployment concern.
if (builder.Configuration.GetValue<bool>("Http:UseHttpsRedirection")) app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/ready", async (SalesDbContext db, CancellationToken cancellationToken) =>
{
	try
	{
		if (await db.Database.CanConnectAsync(cancellationToken)
		    && !(await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
			return Results.Ok(new { status = "healthy" });
	}
	catch (Exception ex) when (ex is not OperationCanceledException)
	{
		app.Logger.LogWarning("Database readiness check failed ({ExceptionType}).", ex.GetType().Name);
	}

	return Results.Problem(statusCode: 503, title: "Database is not ready.");
});

await app.RunAsync();

public abstract partial class Program { }
