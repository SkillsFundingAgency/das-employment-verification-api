using Microsoft.Extensions.Configuration;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public sealed class MessagingInfrastructureOptions
{
    public bool Enabled { get; init; }
    public string TopicName { get; init; } = "bundle-1";
    public string QueueName { get; init; } = string.Empty;
    public string SubscriptionName { get; init; } = string.Empty;
    public const string RuleName = "EvsMessages";
    // Broker prefilter: exact concrete-type identity is enforced by the consumer.
    // These two CLR names contain no LIKE wildcard characters. Preserve the deployed SQL.
    public static readonly string SqlFilter =
        $"[{ServiceBusMessageHeaders.EnclosedMessageTypes}] LIKE '%{typeof(VerifyNotEmployed).FullName}%' OR " +
        $"[{ServiceBusMessageHeaders.EnclosedMessageTypes}] LIKE '%{typeof(EmploymentCheckCompletedEvent).FullName}%'";

    public static MessagingInfrastructureOptions Read(IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("ServiceBusSetupEnabled");
        var topic = configuration["DasSharedServiceBusTopicName"] ?? "bundle-1";
        if (string.IsNullOrWhiteSpace(topic))
            throw new InvalidOperationException("DasSharedServiceBusTopicName must not be empty.");
        return new MessagingInfrastructureOptions
        {
            Enabled = enabled,
            TopicName = topic,
            QueueName = enabled ? Required(configuration, "EvsQueueName") : string.Empty,
            SubscriptionName = enabled ? Required(configuration, "EvsSubscriptionName") : string.Empty
        };
    }

    private static string Required(IConfiguration configuration, string name) =>
        !string.IsNullOrWhiteSpace(configuration[name]) ? configuration[name]! :
            throw new InvalidOperationException($"The {name} setting is required for messaging provisioning.");
}
