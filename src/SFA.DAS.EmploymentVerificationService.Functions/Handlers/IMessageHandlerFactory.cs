using Azure.Messaging.ServiceBus;

namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public interface IMessageHandlerFactory
{
    Task HandleAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken);
}
