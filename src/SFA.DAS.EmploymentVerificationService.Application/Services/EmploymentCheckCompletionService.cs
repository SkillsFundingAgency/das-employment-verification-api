using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public sealed class EmploymentCheckCompletionService(
    IEmploymentCheckRepository repository,
    IEmploymentVerificationPublisher publisher,
    TimeProvider timeProvider,
    ILogger<EmploymentCheckCompletionService> logger) : IEmploymentCheckCompletionService
{
    public async Task ProcessAsync(EmploymentCheckResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = await repository.GetRequestAsync(result.CorrelationId, cancellationToken);
        if (request is null)
        {
            logger.LogWarning("No matching request found for {CorrelationId}", result.CorrelationId);
            return;
        }

        if (result.EmploymentResult is not bool employed || !string.IsNullOrEmpty(result.ErrorType))
        {
            throw new InvalidOperationException("Employment check failure processing is not implemented.");
        }

        await repository.RecordCompletionAsync(new EmploymentCheckCompletion(
            request.CorrelationId,
            employed,
            (byte)EmploymentCheckStatus.Completed,
            timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        logger.LogInformation("Employment check result persisted for {CorrelationId}", result.CorrelationId);

        await publisher.PublishAsync(new VerifyNotEmployedCompleted
        {
            CorrelationId = request.CorrelationId,
            ULN = request.ULN,
            ApprenticeshipId = request.ApprenticeshipId,
            EmployerAccountIdentifier = request.EmployerAccountIdentifier,
            NotEmployed = !employed
        }, cancellationToken);
        logger.LogInformation("VerifyNotEmployedCompleted event published for {CorrelationId}", result.CorrelationId);
    }
}
