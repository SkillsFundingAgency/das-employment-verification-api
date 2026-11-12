using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using SFA.DAS.EmploymentVerificationService.Functions.Handlers;

namespace SFA.DAS.EmploymentVerificationService.Functions.Triggers;

public class EmploymentVerificationServiceBusFunction(IMessageHandlerFactory factory)
{
    [Function(nameof(EmploymentVerificationServiceBusFunction))]
    public Task Run(
        [ServiceBusTrigger("%EvsQueueName%", Connection = "DasSharedServiceBusConnection", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken) => factory.HandleAsync(message, cancellationToken);
}
