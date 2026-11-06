using Microsoft.Extensions.Configuration;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public static class EvsRoutes
{
    // LOCAL TESTING ONLY: temporary filter for local infrastructure testing.
    // TODO: Confirm DataLocks publishing metadata before using this filter outside local testing.
    private const string VerifyNotEmployedSqlFilter =
        "sys.Label = 'VerifyNotEmployed'";
    private const string EmploymentCheckCompletedSqlFilter =
        "[NServiceBus.EnclosedMessageTypes] LIKE '%SFA.DAS.EmploymentCheck.Types.EmploymentCheckCompletedEvent%'";

    public static IReadOnlyList<MessagingRoute> For(IConfiguration configuration)
    {
        var topicName = Required(configuration, "ServiceBusTopicName");
        return
        [
            new MessagingRoute
            {
                RuleName = nameof(VerifyNotEmployed),
                TopicName = topicName,
                QueueName = Required(configuration, "VerifyNotEmployedQueueName"),
                SubscriptionName = Required(configuration, "VerifyNotEmployedSubscriptionName"),
                SqlFilter = VerifyNotEmployedSqlFilter
            },
            new MessagingRoute
            {
                RuleName = "EmploymentCheckCompletedEvent",
                TopicName = topicName,
                QueueName = Required(configuration, "EmploymentCheckCompletedQueueName"),
                SubscriptionName = Required(configuration, "EmploymentCheckCompletedSubscriptionName"),
                SqlFilter = EmploymentCheckCompletedSqlFilter
            }
        ];
    }

    private static string Required(IConfiguration configuration, string settingName)
    {
        var value = configuration[settingName];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"The {settingName} setting is required for messaging provisioning.");
        return value;
    }
}
