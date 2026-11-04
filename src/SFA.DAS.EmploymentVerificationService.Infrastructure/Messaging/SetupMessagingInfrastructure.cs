using Azure;
using Azure.Messaging.ServiceBus;
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
        foreach (var route in options.Routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await EnsureRouteAsync(route, cancellationToken);
                logger.LogInformation("Ensured EVS messaging route {Topic}/{Subscription} to {Queue}",
                    route.TopicName, route.SubscriptionName, route.QueueName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not provision EVS route {Topic}/{Subscription}", route.TopicName, route.SubscriptionName);
                throw;
            }
        }
    }

    private async Task EnsureRouteAsync(MessagingRoute route, CancellationToken cancellationToken)
    {
        if (!(await client.TopicExistsAsync(route.TopicName, cancellationToken)).Value)
            throw new InvalidOperationException($"Shared topic '{route.TopicName}' does not exist. EVS does not create shared topics.");

        if (!(await client.QueueExistsAsync(route.QueueName, cancellationToken)).Value)
        {
            await CreateIfMissingAsync(async () =>
            {
                await client.CreateQueueAsync(new CreateQueueOptions(route.QueueName)
                {
                    DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                    DeadLetteringOnMessageExpiration = true,
                    LockDuration = TimeSpan.FromMinutes(5),
                    MaxDeliveryCount = 50,
                    MaxSizeInMegabytes = 5120
                }, cancellationToken);
            });
            if (!(await client.QueueExistsAsync(route.QueueName, cancellationToken)).Value)
                throw new InvalidOperationException($"Queue '{route.QueueName}' does not exist after provisioning.");
        }

        if (!(await client.SubscriptionExistsAsync(route.TopicName, route.SubscriptionName, cancellationToken)).Value)
            await CreateIfMissingAsync(async () =>
            {
                await client.CreateSubscriptionAsync(new CreateSubscriptionOptions(route.TopicName, route.SubscriptionName)
                {
                    ForwardTo = route.QueueName,
                    DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                    DeadLetteringOnMessageExpiration = true,
                    LockDuration = TimeSpan.FromMinutes(5),
                    MaxDeliveryCount = 50
                }, new CreateRuleOptions(route.RuleName, new SqlRuleFilter(route.SqlFilter)), cancellationToken);
            });

        // Always reconcile, including when another host won a create race.
        var rules = await ReadRulesAsync(route, cancellationToken);
        if (rules.Any(rule => rule.Name != route.RuleName && !IsDefault(rule.Name)))
            throw new InvalidOperationException($"Subscription '{route.SubscriptionName}' contains unexpected rules; review them before provisioning.");

        var requiredRule = rules.SingleOrDefault(rule => rule.Name == route.RuleName);
        if (requiredRule is null)
        {
            await CreateIfMissingAsync(async () =>
            {
                await client.CreateRuleAsync(route.TopicName, route.SubscriptionName,
                    new CreateRuleOptions(route.RuleName, new SqlRuleFilter(route.SqlFilter)), cancellationToken);
            });
            requiredRule = (await client.GetRuleAsync(route.TopicName, route.SubscriptionName, route.RuleName, cancellationToken)).Value;
        }
        if (!IsRequiredRule(requiredRule, route))
        {
            requiredRule.Filter = new SqlRuleFilter(route.SqlFilter);
            requiredRule.Action = null;
            await client.UpdateRuleAsync(route.TopicName, route.SubscriptionName, requiredRule, cancellationToken);
        }

        foreach (var rule in rules.Where(rule => IsDefault(rule.Name)))
        {
            try { await client.DeleteRuleAsync(route.TopicName, route.SubscriptionName, rule.Name, cancellationToken); }
            catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound) { }
            catch (RequestFailedException exception) when (exception.Status == 404) { }
        }

        var subscription = (await client.GetSubscriptionAsync(route.TopicName, route.SubscriptionName, cancellationToken)).Value;
        if (!string.Equals(subscription.ForwardTo, route.QueueName, StringComparison.OrdinalIgnoreCase))
        {
            subscription.ForwardTo = route.QueueName;
            await client.UpdateSubscriptionAsync(subscription, cancellationToken);
        }

        var finalRules = await ReadRulesAsync(route, cancellationToken);
        subscription = (await client.GetSubscriptionAsync(route.TopicName, route.SubscriptionName, cancellationToken)).Value;
        if (finalRules.Count != 1 || !IsRequiredRule(finalRules[0], route)
            || !string.Equals(subscription.ForwardTo, route.QueueName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Subscription '{route.SubscriptionName}' did not retain the required forwarding and exclusive filter configuration.");
    }

    private async Task<List<RuleProperties>> ReadRulesAsync(MessagingRoute route, CancellationToken cancellationToken)
    {
        var rules = new List<RuleProperties>();
        await foreach (var rule in client.GetRulesAsync(route.TopicName, route.SubscriptionName, cancellationToken))
            rules.Add(rule);
        return rules;
    }

    private static bool IsDefault(string name) => name.Equals(RuleProperties.DefaultRuleName, StringComparison.OrdinalIgnoreCase);

    private static bool IsRequiredRule(RuleProperties rule, MessagingRoute route) =>
        rule.Name == route.RuleName && rule.Filter is SqlRuleFilter filter && filter.SqlExpression == route.SqlFilter
        && rule.Action is null;

    private static async Task CreateIfMissingAsync(Func<Task> create)
    {
        try { await create(); }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists) { }
        catch (RequestFailedException exception) when (exception.Status == 409) { }
        // The caller reads the entity back; a conflict alone never establishes correctness.
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
