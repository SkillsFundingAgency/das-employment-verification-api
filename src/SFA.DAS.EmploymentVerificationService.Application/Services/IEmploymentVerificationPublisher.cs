using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public interface IEmploymentVerificationPublisher
{
    Task PublishAsync(VerifyNotEmployedCompleted message, CancellationToken cancellationToken);
}
