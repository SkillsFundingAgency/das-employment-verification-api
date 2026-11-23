using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;
using SFA.DAS.EmploymentVerificationService.Models;
using EmploymentCheckStatus = SFA.DAS.EmploymentVerificationService.Models.EmploymentCheckStatus;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public sealed class EmploymentCheckCompletionService(
    IEmploymentCheckRepository repository,
    IMessagePublisher publisher,
    TimeProvider timeProvider,
    ILogger<EmploymentCheckCompletionService> logger) : IEmploymentCheckCompletionService
{
    public async Task ProcessAsync(EmploymentCheckResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var responseTime = timeProvider.GetUtcNow().UtcDateTime;

        var verification = await repository.GetVerificationForCheckAsync(result.CorrelationId, cancellationToken);
        if (verification is null)
        {
            logger.LogWarning("No matching request found for {CorrelationId}", result.CorrelationId);
            return;
        }

        var check = verification.LearnerEmploymentChecks
            .Single(check => check.Detail.CheckCorrelationId == result.CorrelationId).Detail;
        check.ResponseTime = responseTime;

        if (result.EmploymentResult is not bool employed || !string.IsNullOrEmpty(result.ErrorType))
        {
            const int maxFailureReasonLength = 200;
            check.Status = (byte)EmploymentCheckStatus.Failed;
            check.FailureReason = result.ErrorType is { Length: > maxFailureReasonLength } errorType
                ? errorType[..maxFailureReasonLength]
                : result.ErrorType;
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Employment check failure persisted for {CorrelationId}", result.CorrelationId);
            return;
        }

        check.Status = (byte)(employed ? EmploymentCheckStatus.Employed : EmploymentCheckStatus.NotEmployed);
        check.FailureReason = null;
        verification.Detail.Status = employed
            ? EmploymentVerificationStatus.Employed
            : EmploymentVerificationStatus.NotEmployed;

        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Employment check result persisted for {CorrelationId}", result.CorrelationId);

        await publisher.PublishAsync(new VerifyNotEmployedCompleted
        {
            CorrelationId = verification.Detail.ExternalCorrelationId,
            ULN = verification.UniqueLearnerNumber,
            ApprenticeshipId = verification.ApprenticeshipId,
            EmployerAccountIdentifier = verification.EmployerAccountId,
            NotEmployed = !employed
        }, cancellationToken);
        logger.LogInformation("VerifyNotEmployedCompleted event published for {CorrelationId}", result.CorrelationId);
    }
}
