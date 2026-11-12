using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public sealed class SetupMessagingInfrastructure(
    ServiceBusAdministrationClient client,
    MessagingInfrastructureOptions options,
    ILogger<SetupMessagingInfrastructure> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled) return;
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureRouteAsync(options, cancellationToken);
        logger.LogInformation("Ensured EVS messaging route {Topic}/{Subscription} to {Queue}",
            options.TopicName, options.SubscriptionName, options.QueueName);
    }

    private async Task EnsureRouteAsync(MessagingInfrastructureOptions route, CancellationToken cancellationToken)
    {
        if (!(await client.TopicExistsAsync(route.TopicName, cancellationToken)).Value)
            throw new InvalidOperationException($"Shared topic '{route.TopicName}' does not exist. EVS does not create shared topics.");

        if (!(await client.QueueExistsAsync(route.QueueName, cancellationToken)).Value)
        {
            await client.CreateQueueAsync(new CreateQueueOptions(route.QueueName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                DeadLetteringOnMessageExpiration = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = 50,
                MaxSizeInMegabytes = 5120
            }, cancellationToken);
        }

        if (!(await client.SubscriptionExistsAsync(route.TopicName, route.SubscriptionName, cancellationToken)).Value)
        {
            await client.CreateSubscriptionAsync(new CreateSubscriptionOptions(route.TopicName, route.SubscriptionName)
            {
                ForwardTo = route.QueueName,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                DeadLetteringOnMessageExpiration = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = 50
            }, new CreateRuleOptions(MessagingInfrastructureOptions.RuleName, new SqlRuleFilter(MessagingInfrastructureOptions.SqlFilter)), cancellationToken);
            return;
        }

        var subscription = (await client.GetSubscriptionAsync(route.TopicName, route.SubscriptionName, cancellationToken)).Value;
        var forwardTo = Uri.TryCreate(subscription.ForwardTo, UriKind.Absolute, out var destination)
            ? destination.Segments.Last().TrimEnd('/')
            : subscription.ForwardTo;
        if (!string.Equals(forwardTo, route.QueueName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Subscription '{route.TopicName}/{route.SubscriptionName}' forwards to '{subscription.ForwardTo}' instead of queue '{route.QueueName}' in the configured Service Bus namespace. Correct the forwarding destination before starting EVS.");

        RuleProperties? requiredRule = null;
        RuleProperties? defaultRule = null;
        await foreach (var rule in client.GetRulesAsync(route.TopicName, route.SubscriptionName, cancellationToken))
        {
            if (rule.Name == MessagingInfrastructureOptions.RuleName)
                requiredRule = rule;
            else if (rule.Name.Equals(RuleProperties.DefaultRuleName, StringComparison.OrdinalIgnoreCase))
                defaultRule = rule;
            else
                throw new InvalidOperationException(
                    $"Subscription '{route.TopicName}/{route.SubscriptionName}' contains unexpected rule '{rule.Name}'. Review its rules before starting EVS.");
        }

        if (requiredRule is null)
            throw new InvalidOperationException(
                $"Subscription '{route.TopicName}/{route.SubscriptionName}' is missing required SQL filter rule '{MessagingInfrastructureOptions.RuleName}'. Configure it before starting EVS.");
        if (requiredRule.Filter is not SqlRuleFilter filter || filter.SqlExpression != MessagingInfrastructureOptions.SqlFilter || requiredRule.Action is not null)
            throw new InvalidOperationException(
                $"Subscription '{route.TopicName}/{route.SubscriptionName}' rule '{MessagingInfrastructureOptions.RuleName}' must use SQL filter '{MessagingInfrastructureOptions.SqlFilter}' with no rule action. Correct the rule before starting EVS.");

        // Remove the default rule
        if (defaultRule is not null)
            await client.DeleteRuleAsync(route.TopicName, route.SubscriptionName, defaultRule.Name, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
