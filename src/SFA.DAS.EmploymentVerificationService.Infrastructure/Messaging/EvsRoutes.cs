using Microsoft.Extensions.Configuration;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public static class EvsRoutes
{
    private static readonly string VerifyNotEmployedSqlFilter =
        $"[NServiceBus.EnclosedMessageTypes] LIKE '%{typeof(VerifyNotEmployed).FullName}%'";
    private const string EmploymentCheckCompletedSqlFilter =
        "[NServiceBus.EnclosedMessageTypes] LIKE '%SFA.DAS.EmploymentCheck.Types.EmploymentCheckCompletedEvent%'";

    public static IReadOnlyList<MessagingRoute> For(IConfiguration configuration)
    {
        return
        [
            new MessagingRoute
            {
                RuleName = nameof(VerifyNotEmployed),
                Namespace = ServiceBusNamespace.Payments,
                TopicName = Required(configuration, "PaymentsServiceBusTopicName"),
                QueueName = Required(configuration, "VerifyNotEmployedQueueName"),
                SubscriptionName = Required(configuration, "VerifyNotEmployedSubscriptionName"),
                SqlFilter = VerifyNotEmployedSqlFilter
            },
            new MessagingRoute
            {
                RuleName = "EmploymentCheckCompletedEvent",
                Namespace = ServiceBusNamespace.DasShared,
                TopicName = Required(configuration, "DasSharedServiceBusTopicName"),
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
