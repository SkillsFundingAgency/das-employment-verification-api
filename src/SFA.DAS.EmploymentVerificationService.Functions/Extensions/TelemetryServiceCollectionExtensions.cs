using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry;

namespace SFA.DAS.EmploymentVerificationService.Functions.Extensions;

public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddEmploymentVerificationTelemetry(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Microsoft", LogLevel.Warning));
        services.Configure<OpenTelemetryLoggerOptions>(options => options.IncludeScopes = true);

        var telemetry = services.AddOpenTelemetry().UseFunctionsWorkerDefaults().WithLogging();
        var connectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        // Keep local telemetry in-process unless an export destination is explicitly configured.
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            telemetry.UseAzureMonitorExporter(options => options.ConnectionString = connectionString);
        }

        return services;
    }
}
