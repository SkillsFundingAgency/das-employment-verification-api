using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;

namespace SFA.DAS.EmploymentVerificationService.Functions.Triggers;

public class EmploymentVerificationServiceBusTrigger(IMessageProcessorFactory factory)
{
    [Function(nameof(EmploymentVerificationServiceBusTrigger))]
    public Task Run(
        [ServiceBusTrigger("%EvsQueueName%", Connection = "DasSharedServiceBusConnection", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken) => factory.ProcessAsync(message, cancellationToken);
}
