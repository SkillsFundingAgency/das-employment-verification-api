namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public interface IMessageHandler<TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
