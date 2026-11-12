namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public interface IMessageHandler
{
    Task HandleAsync(BinaryData body, CancellationToken cancellationToken);
}

public interface IMessageHandler<TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
