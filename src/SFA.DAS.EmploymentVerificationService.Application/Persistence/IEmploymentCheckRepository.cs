using SFA.DAS.EmploymentVerificationService.Application.Models;

namespace SFA.DAS.EmploymentVerificationService.Application.Persistence;

public interface IEmploymentCheckRepository
{
    Task<bool> VerificationExists(Guid externalCorrelationId, CancellationToken cancellationToken);
    Task SaveRequestAsync(EmploymentCheckRequest request, CancellationToken cancellationToken);
    Task<EmploymentCheckRequest?> GetRequestAsync(Guid correlationId, CancellationToken cancellationToken);
    Task RecordCompletionAsync(EmploymentCheckCompletion completion, CancellationToken cancellationToken);
}
