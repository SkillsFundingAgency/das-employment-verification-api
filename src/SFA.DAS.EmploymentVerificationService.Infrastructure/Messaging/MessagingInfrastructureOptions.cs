using Microsoft.Extensions.Configuration;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public sealed class MessagingRoute
{
    public string RuleName { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string SqlFilter { get; set; } = string.Empty;
}

public sealed class MessagingInfrastructureOptions
{
    public MessagingInfrastructureOptions(bool enabled, IReadOnlyList<MessagingRoute> routes)
    {
        Enabled = enabled;
        Routes = routes;
    }

    public bool Enabled { get; }
    public IReadOnlyList<MessagingRoute> Routes { get; }

    public static MessagingInfrastructureOptions Read(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ServiceBusSetupEnabled"))
            return new(false, Array.Empty<MessagingRoute>());

        if (!configuration.GetValue<bool>("ServiceBusFiltersReviewed"))
            throw new InvalidOperationException("ServiceBusFiltersReviewed must be true before enabling messaging provisioning.");

        var routes = EvsRoutes.For(configuration);

        var queues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var subscriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes)
        {
            if (string.IsNullOrWhiteSpace(route.RuleName) || string.IsNullOrWhiteSpace(route.TopicName)
                || string.IsNullOrWhiteSpace(route.SubscriptionName) || string.IsNullOrWhiteSpace(route.QueueName))
                throw new InvalidOperationException("Each messaging route requires RuleName, TopicName, SubscriptionName and QueueName.");

            if (route.RuleName.Equals("$Default", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Messaging routes must use a named SQL rule other than $Default.");

            if (!queues.Add(route.QueueName) || !subscriptions.Add($"{route.TopicName}/{route.SubscriptionName}"))
                throw new InvalidOperationException("Messaging routes must use distinct queues and distinct topic/subscription pairs.");
        }

        foreach (var route in routes)
        {
            if (string.IsNullOrWhiteSpace(route.SqlFilter))
                throw new InvalidOperationException(route.RuleName == "VerifyNotEmployed"
                    ? "The VerifyNotEmployed SQL filter in EvsRoutes is unresolved. DataLocks publishing metadata must be confirmed before enabling ServiceBusSetupEnabled."
                    : $"The SQL filter for messaging rule '{route.RuleName}' is required for messaging provisioning.");
        }

        return new(true, routes);
    }
}
