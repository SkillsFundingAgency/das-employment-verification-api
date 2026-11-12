using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;
using System.Collections.ObjectModel;

namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public sealed class MessageHandlerFactory : IMessageHandlerFactory
{
    private static readonly IReadOnlyDictionary<string, Type> Handlers =
        new ReadOnlyDictionary<string, Type>(new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [typeof(VerifyNotEmployed).FullName!] = typeof(VerifyNotEmployedHandler),
            [typeof(EmploymentCheckCompletedEvent).FullName!] = typeof(EmploymentCheckCompletedEventHandler)
        });

    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<MessageHandlerFactory> logger;

    public MessageHandlerFactory(IServiceProvider serviceProvider, ILogger<MessageHandlerFactory> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task HandleAsync(
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
        Type? handlerType = null;
        foreach (var entry in enclosedMessageTypes.Split(';'))
        {
            var messageType = entry.Split(',')[0].Trim();
            if (!Handlers.TryGetValue(messageType, out var candidate))
                continue;

            if (selectedType is not null && !string.Equals(selectedType, messageType, StringComparison.Ordinal))
                throw new InvalidOperationException("Message metadata contains different supported message types.");

            selectedType = messageType;
            handlerType = candidate;
        }

        if (handlerType is null)
            throw new InvalidOperationException("No supported message type was found in the metadata.");

        logger.LogInformation(
            "Processing {MessageType}, MessageId {MessageId}",
            selectedType, message.MessageId);

        var handler = (IMessageHandler)serviceProvider.GetRequiredService(handlerType);
        await handler.HandleAsync(message.Body, cancellationToken);
    }
}
