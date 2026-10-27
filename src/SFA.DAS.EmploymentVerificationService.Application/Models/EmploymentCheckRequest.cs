namespace SFA.DAS.EmploymentVerificationService.Application.Models;

public sealed record EmploymentCheckRequest(
    Guid CorrelationId,
    long ULN,
    long AccountId,
    long? ApprenticeshipId,
    string? Reference,
    DateTime StartDate,
    DateTime EndDate,
    int NumberOfAttempts,
    byte Status,
    DateTime CreatedOn);
