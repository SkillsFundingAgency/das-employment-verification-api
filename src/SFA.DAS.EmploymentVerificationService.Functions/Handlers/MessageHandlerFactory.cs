using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Functions.Handlers;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;
using System.Text.Json;

public sealed class MessageHandlerFactory(
    IServiceProvider serviceProvider,
    ILogger<MessageHandlerFactory> logger) : IMessageHandlerFactory
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public async Task HandleAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        if (!message.ApplicationProperties.TryGetValue(
                ServiceBusMessageHeaders.EnclosedMessageTypes,
                out var metadata))
        {
            throw new InvalidOperationException("Message type is missing.");
        }

        var messageType = metadata.ToString()?.Split(',')[0].Trim();

        logger.LogInformation(
            "Processing {MessageType}, MessageId {MessageId}",
            messageType, message.MessageId);

        try
        {
            switch (messageType)
            {
                case var type when type == typeof(VerifyNotEmployed).FullName:
                    await HandleAsync<VerifyNotEmployed>(
                        message.Body, cancellationToken);
                    break;

                case var type when type == typeof(EmploymentCheckCompletedEvent).FullName:
                    await HandleAsync<EmploymentCheckCompletedEvent>(
                        message.Body, cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported message type: {messageType}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process {MessageType}, MessageId {MessageId}",
                messageType, message.MessageId);

            throw;
        }
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