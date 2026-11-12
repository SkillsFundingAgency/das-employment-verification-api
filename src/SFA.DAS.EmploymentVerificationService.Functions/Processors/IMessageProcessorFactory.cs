using Azure.Messaging.ServiceBus;

namespace SFA.DAS.EmploymentVerificationService.Functions.Processors;

public interface IMessageProcessorFactory
{
    Task ProcessAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken);
}
