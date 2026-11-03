using System.Text.Json;
using Azure.Messaging.ServiceBus;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Services;

public sealed class EmploymentVerificationPublisher(ServiceBusSender sender) : IEmploymentVerificationPublisher
{
    public Task PublishAsync(VerifyNotEmployedCompleted message, CancellationToken cancellationToken)
    {
        var serviceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(message))
        {
            ContentType = "application/json",
            Subject = nameof(VerifyNotEmployedCompleted),
            CorrelationId = message.CorrelationId.ToString()
        };

        return sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}
