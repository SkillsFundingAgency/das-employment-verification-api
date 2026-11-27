namespace SFA.DAS.EmploymentVerificationService.Application.Models;

public sealed record EmploymentCheckResult(
    Guid CorrelationId,
    bool? EmploymentResult,
    DateTime CheckDate,
    string? ErrorType);
