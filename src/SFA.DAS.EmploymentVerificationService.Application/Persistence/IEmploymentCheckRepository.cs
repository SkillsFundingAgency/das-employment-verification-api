using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Application.Persistence;

public interface IEmploymentCheckRepository
{
    Task<bool> VerificationExists(Guid externalCorrelationId, CancellationToken cancellationToken);
    Task SaveVerificationAsync(LearnerEmploymentVerification verification, CancellationToken cancellationToken);
    Task<LearnerEmploymentVerification?> GetVerificationForCheckAsync(Guid checkCorrelationId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
