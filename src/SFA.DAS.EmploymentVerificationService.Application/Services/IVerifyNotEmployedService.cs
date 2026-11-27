using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public interface IVerifyNotEmployedService
{
    Task ProcessAsync(VerifyNotEmployed command, CancellationToken cancellationToken);
}
