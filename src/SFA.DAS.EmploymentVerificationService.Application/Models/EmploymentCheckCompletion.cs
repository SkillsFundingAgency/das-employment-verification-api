namespace SFA.DAS.EmploymentVerificationService.Application.Models;

public sealed record EmploymentCheckCompletion(
    Guid CorrelationId,
    bool Employed,
    byte UpdatedStatus,
    DateTime CreatedOn);
