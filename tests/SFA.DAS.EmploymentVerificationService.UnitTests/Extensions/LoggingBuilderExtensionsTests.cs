using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Functions.Extensions;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Extensions;

[TestFixture]
public class LoggingBuilderExtensionsTests
{
    private static readonly string ProviderName = typeof(ApplicationInsightsLoggerProvider).FullName!;

    [Test]
    public void ApplicationInsightsWorkerService_AddsDefaultWarningFilter()
    {
        using var provider = new ServiceCollection()
            .AddApplicationInsightsTelemetryWorkerService()
            .BuildServiceProvider();

        var rules = provider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value.Rules;

        Assert.That(rules, Has.Some.Matches<LoggerFilterRule>(r =>
            r.ProviderName == ProviderName && r.LogLevel == LogLevel.Warning));
    }

    [Test]
    public void RemoveApplicationInsightsDefaultFilter_RemovesOnlyTheApplicationInsightsRule()
    {
        using var provider = new ServiceCollection()
            .AddLogging(logging => logging.AddFilter("Other.Category", LogLevel.Error))
            .AddApplicationInsightsTelemetryWorkerService()
            .AddLogging(logging => logging.RemoveApplicationInsightsDefaultFilter())
            .BuildServiceProvider();

        var rules = provider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value.Rules;

        Assert.Multiple(() =>
        {
            Assert.That(rules, Has.None.Matches<LoggerFilterRule>(r => r.ProviderName == ProviderName));
            Assert.That(rules, Has.Some.Matches<LoggerFilterRule>(r => r.CategoryName == "Other.Category"));
        });
    }
}
