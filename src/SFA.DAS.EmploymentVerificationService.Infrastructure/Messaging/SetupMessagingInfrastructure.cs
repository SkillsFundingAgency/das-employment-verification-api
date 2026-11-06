using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public sealed class SetupMessagingInfrastructure(
    IReadOnlyDictionary<ServiceBusNamespace, ServiceBusAdministrationClient> administrationClients,
    MessagingInfrastructureOptions options,
    ILogger<SetupMessagingInfrastructure> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled) return;
        MessagingInfrastructureOptions.ValidateRoutes(options.Routes);
        foreach (var route in options.Routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var client = administrationClients[route.Namespace];
                await EnsureRouteAsync(client, route, cancellationToken);
                logger.LogInformation("Ensured EVS messaging route {Namespace}: {Topic}/{Subscription} to {Queue}",
                    route.Namespace, route.TopicName, route.SubscriptionName, route.QueueName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not provision EVS route {Namespace}: {Topic}/{Subscription}", route.Namespace, route.TopicName, route.SubscriptionName);
                throw;
            }
        }
    }

    private async Task EnsureRouteAsync(ServiceBusAdministrationClient client,
        MessagingRoute route, CancellationToken cancellationToken)
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
            }, new CreateRuleOptions(route.RuleName, new SqlRuleFilter(route.SqlFilter)), cancellationToken);
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
            if (rule.Name == route.RuleName)
                requiredRule = rule;
            else if (rule.Name.Equals(RuleProperties.DefaultRuleName, StringComparison.OrdinalIgnoreCase))
                defaultRule = rule;
            else
                throw new InvalidOperationException(
                    $"Subscription '{route.TopicName}/{route.SubscriptionName}' contains unexpected rule '{rule.Name}'. Review its rules before starting EVS.");
        }

        if (requiredRule is null)
            throw new InvalidOperationException(
                $"Subscription '{route.TopicName}/{route.SubscriptionName}' is missing required SQL filter rule '{route.RuleName}'. Configure it before starting EVS.");
        if (requiredRule.Filter is not SqlRuleFilter filter || filter.SqlExpression != route.SqlFilter || requiredRule.Action is not null)
            throw new InvalidOperationException(
                $"Subscription '{route.TopicName}/{route.SubscriptionName}' rule '{route.RuleName}' must use SQL filter '{route.SqlFilter}' with no rule action. Correct the rule before starting EVS.");

        // Remove the catch-all rule
        if (defaultRule is not null)
            await client.DeleteRuleAsync(route.TopicName, route.SubscriptionName, defaultRule.Name, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
