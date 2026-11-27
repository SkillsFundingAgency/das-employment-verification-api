using Azure.Messaging.ServiceBus;

namespace SFA.DAS.EmploymentVerificationService.Functions.Processors;

public interface IMessageProcessorFactory
{
    IMessageProcessor GetProcessor(ServiceBusReceivedMessage message);
}
