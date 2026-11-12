using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;
using System.Text.Json;

namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public sealed class MessageHandlerFactory : IMessageHandlerFactory
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<MessageHandlerFactory> logger;
    private readonly IReadOnlyDictionary<string, Func<BinaryData, CancellationToken, Task>> handlers;

    public MessageHandlerFactory(
        IServiceProvider serviceProvider,
        ILogger<MessageHandlerFactory> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        handlers = new Dictionary<string, Func<BinaryData, CancellationToken, Task>>(StringComparer.Ordinal)
        {
            [typeof(VerifyNotEmployed).FullName!] = HandleAsync<VerifyNotEmployed>,
            [typeof(EmploymentCheckCompletedEvent).FullName!] = HandleAsync<EmploymentCheckCompletedEvent>
        };
    }

    public async Task HandleAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["DeliveryCount"] = message.DeliveryCount
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
        Func<BinaryData, CancellationToken, Task>? selectedHandler = null;
        foreach (var entry in enclosedMessageTypes.Split(';'))
        {
            var messageType = entry.Split(',')[0].Trim();
            if (!handlers.TryGetValue(messageType, out var candidate))
                continue;

            if (selectedType is not null && !string.Equals(selectedType, messageType, StringComparison.Ordinal))
                throw new InvalidOperationException("Message metadata contains different supported message types.");

            selectedType = messageType;
            selectedHandler = candidate;
        }

        if (selectedHandler is null)
            throw new InvalidOperationException("No supported message type was found in the metadata.");

        logger.LogInformation(
            "Processing {MessageType}, MessageId {MessageId}",
            selectedType, message.MessageId);

        await selectedHandler(message.Body, cancellationToken);
    }

    private Task HandleAsync<TMessage>(
        BinaryData body,
        CancellationToken cancellationToken) where TMessage : class
    {
        var handler =
            serviceProvider.GetRequiredService<IMessageHandler<TMessage>>();

        var command = JsonSerializer.Deserialize<TMessage>(
            body.ToMemory().Span, JsonOptions)
            ?? throw new JsonException("Invalid message body.");

        return handler.HandleAsync(command, cancellationToken);
    }
}
