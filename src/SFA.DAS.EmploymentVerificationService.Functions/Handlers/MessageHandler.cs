using System.Text.Json;

namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public abstract class MessageHandler<TMessage> : IMessageHandler where TMessage : class
{
    public Task HandleAsync(BinaryData body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = JsonSerializer.Deserialize<TMessage>(body.ToMemory().Span, MessageHandlerJson.Options)
            ?? throw new JsonException("Invalid message body.");

        return HandleAsync(message, cancellationToken);
    }

    protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}

internal static class MessageHandlerJson
{
    internal static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
