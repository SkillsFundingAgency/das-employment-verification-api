namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken, bool includeEnclosedMessageTypes = true)
        where TMessage : class;
}
