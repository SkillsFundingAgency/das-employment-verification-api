using SFA.DAS.EmploymentVerificationService.Application.Models;

namespace SFA.DAS.EmploymentVerificationService.Application.Persistence;

public interface IEmploymentCheckRepository
{
    Task<bool> ExistsAsync(Guid correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a new request. The existence check alone does not guarantee uniqueness
    /// when requests are processed concurrently.
    /// </summary>
    Task SaveRequestAsync(EmploymentCheckRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the request with the supplied correlation ID, or null if it does not exist.
    /// </summary>
    Task<EmploymentCheckRequest?> GetRequestAsync(Guid correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the completed response and updates the matching request's status atomically.
    /// If either write fails, neither change may be persisted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The matching request does not exist.</exception>
    Task RecordCompletionAsync(EmploymentCheckCompletion completion, CancellationToken cancellationToken);
}
