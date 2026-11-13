namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class;
}
