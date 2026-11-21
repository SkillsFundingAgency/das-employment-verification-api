using SFA.DAS.EmploymentVerificationService.Application.Models;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public interface IEmploymentCheckCompletionService
{
    Task ProcessAsync(EmploymentCheckResult result, CancellationToken cancellationToken);
}
