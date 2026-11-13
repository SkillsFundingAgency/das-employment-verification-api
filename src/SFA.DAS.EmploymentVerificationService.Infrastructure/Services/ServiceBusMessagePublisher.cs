using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Services;

public sealed class ServiceBusMessagePublisher(ServiceBusSender sender) : IMessagePublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class
    {
        var serviceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(message))
        {
            ContentType = "application/json"
        };

        serviceBusMessage.ApplicationProperties[ServiceBusMessageHeaders.EnclosedMessageTypes] =
            typeof(TMessage).AssemblyQualifiedName!;

        return sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }
}
