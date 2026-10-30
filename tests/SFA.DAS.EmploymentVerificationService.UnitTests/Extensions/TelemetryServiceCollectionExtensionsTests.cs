using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Logs;
using SFA.DAS.EmploymentVerificationService.Functions.Extensions;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Extensions;

[TestFixture]
public class TelemetryServiceCollectionExtensionsTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t")]
    public async Task Telemetry_WithoutConnectionString_AllowsLocalHostStartup(string? connectionString)
    {
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddEmploymentVerificationTelemetry(Configuration(connectionString)))
            .Build();

        await host.StartAsync();
        Assert.That(host.Services.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>().Count(), Is.EqualTo(1));
        Assert.That(host.Services.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value.IncludeScopes, Is.True);
        // The exporter-specific options are registered only when export is configured.
        Assert.That(host.Services.GetServices<IConfigureOptions<AzureMonitorExporterOptions>>(), Is.Empty);
        await host.StopAsync();
    }

    [Test]
    public void Telemetry_WithConnectionString_ConfiguresAzureMonitor()
    {
        const string connectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000001";
        var services = new ServiceCollection().AddEmploymentVerificationTelemetry(Configuration(connectionString));
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value.ConnectionString,
            Is.EqualTo(connectionString));
        Assert.That(provider.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void Telemetry_CapturesApplicationInformationScopesAndExceptions_AndFiltersFrameworkInformation()
    {
        var exporter = new CapturingExporter();
        var services = new ServiceCollection().AddEmploymentVerificationTelemetry(Configuration(null));
        services.AddOpenTelemetry().WithLogging(logging =>
            logging.AddProcessor(new SimpleLogRecordExportProcessor(exporter)));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();
        var logger = factory.CreateLogger("SFA.DAS.EmploymentVerificationService.Tests");
        var exception = new InvalidOperationException("Test exception");
        using (logger.BeginScope(new Dictionary<string, object?> { ["EmploymentCheckId"] = 123 }))
        {
            logger.LogInformation("Application information");
            logger.LogError(exception, "Application failure");
            factory.CreateLogger("Microsoft.Test").LogInformation("Framework information");
            factory.CreateLogger("Microsoft.Test").LogWarning("Framework warning");
        }

        Assert.That(exporter.Levels, Is.EqualTo(new[] { LogLevel.Information, LogLevel.Error, LogLevel.Warning }));
        Assert.That(exporter.Exception, Is.SameAs(exception));
        Assert.That(exporter.ScopeValues, Does.Contain(new KeyValuePair<string, object?>("EmploymentCheckId", 123)));
    }

    [Test]
    public void HostConfiguration_EnablesOpenTelemetryAndPreservesHttpRoute()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "host.json")));
        var root = json.RootElement;
        Assert.That(root.GetProperty("telemetryMode").GetString(), Is.EqualTo("OpenTelemetry"));
        Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("2.0"));
        Assert.That(root.GetProperty("extensions").GetProperty("http").GetProperty("routePrefix").GetString(), Is.Empty);
        var levels = root.GetProperty("logging").GetProperty("logLevel");
        Assert.That(levels.GetProperty("default").GetString(), Is.EqualTo("Information"));
        Assert.That(levels.GetProperty("Microsoft").GetString(), Is.EqualTo("Warning"));
    }

    private static IConfiguration Configuration(string? connectionString) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = connectionString })
        .Build();

    private sealed class CapturingExporter : BaseExporter<LogRecord>
    {
        public List<LogLevel> Levels { get; } = [];
        public List<KeyValuePair<string, object?>> ScopeValues { get; } = [];
        public Exception? Exception { get; private set; }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                Levels.Add(record.LogLevel);
                Exception = record.Exception ?? Exception;
                record.ForEachScope((scope, values) =>
                {
                    foreach (var value in scope)
                        values.Add(value);
                }, ScopeValues);
            }
            return ExportResult.Success;
        }
    }
}
