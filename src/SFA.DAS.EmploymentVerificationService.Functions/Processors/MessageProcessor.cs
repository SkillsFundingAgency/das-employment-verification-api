using System.Text.Json;

namespace SFA.DAS.EmploymentVerificationService.Functions.Processors;

public abstract class MessageProcessor<TMessage> : IMessageProcessor where TMessage : class
{
    public Task ProcessAsync(BinaryData body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var message = JsonSerializer.Deserialize<TMessage>(body.ToMemory().Span, MessageProcessorJson.Options)
            ?? throw new JsonException("Invalid message body.");

        return ProcessAsync(message, cancellationToken);
    }

    protected abstract Task ProcessAsync(TMessage message, CancellationToken cancellationToken);
}

internal static class MessageProcessorJson
{
    internal static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
