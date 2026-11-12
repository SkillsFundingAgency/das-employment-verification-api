using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;
using System.Collections.ObjectModel;

namespace SFA.DAS.EmploymentVerificationService.Functions.Processors;

public sealed class MessageProcessorFactory : IMessageProcessorFactory
{
    private static readonly IReadOnlyDictionary<string, Type> Processors =
        new ReadOnlyDictionary<string, Type>(new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [typeof(VerifyNotEmployed).FullName!] = typeof(VerifyNotEmployedProcessor),
            [typeof(EmploymentCheckCompletedEvent).FullName!] = typeof(EmploymentCheckCompletedEventProcessor)
        });

    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<MessageProcessorFactory> logger;

    public MessageProcessorFactory(IServiceProvider serviceProvider, ILogger<MessageProcessorFactory> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task ProcessAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["DeliveryCount"] = message.DeliveryCount,
            ["CorrelationId"] = message.CorrelationId
        });

        if (!message.ApplicationProperties.TryGetValue(
                ServiceBusMessageHeaders.EnclosedMessageTypes,
                out var metadata))
        {
            throw new InvalidOperationException("Message type is missing.");
        }

        if (metadata is not string enclosedMessageTypes)
            throw new InvalidOperationException("Message type metadata must be a string.");

        string? selectedType = null;
        Type? processorType = null;
        foreach (var entry in enclosedMessageTypes.Split(';'))
        {
            var messageType = entry.Split(',')[0].Trim();
            if (!Processors.TryGetValue(messageType, out var candidate))
                continue;

            if (selectedType is not null && !string.Equals(selectedType, messageType, StringComparison.Ordinal))
                throw new InvalidOperationException("Message metadata contains different supported message types.");

            selectedType = messageType;
            processorType = candidate;
        }

        if (processorType is null)
            throw new InvalidOperationException("No supported message type was found in the metadata.");

        logger.LogInformation(
            "Processing {MessageType}, MessageId {MessageId}",
            selectedType, message.MessageId);

        var processor = (IMessageProcessor)serviceProvider.GetRequiredService(processorType);
        await processor.ProcessAsync(message.Body, cancellationToken);
    }
}
