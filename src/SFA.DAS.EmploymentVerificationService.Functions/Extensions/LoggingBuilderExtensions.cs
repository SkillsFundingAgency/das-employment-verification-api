using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace SFA.DAS.EmploymentVerificationService.Functions.Extensions;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder RemoveApplicationInsightsDefaultFilter(this ILoggingBuilder logging)
    {
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            var rule = options.Rules.FirstOrDefault(r => r.ProviderName == typeof(ApplicationInsightsLoggerProvider).FullName);
            if (rule is not null)
                options.Rules.Remove(rule);
        });
        return logging;
    }
}
