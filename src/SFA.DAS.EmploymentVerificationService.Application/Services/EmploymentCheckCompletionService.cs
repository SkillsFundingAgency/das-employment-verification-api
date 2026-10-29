using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public sealed class EmploymentCheckCompletionService(
    IEmploymentCheckStore store,
    IEmploymentVerificationPublisher publisher,
    TimeProvider timeProvider)
{
    public async Task ProcessAsync(EmploymentCheckResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = await store.GetRequestAsync(result.CorrelationId, cancellationToken);
        if (request is null)
        {
            return;
        }

        if (result.EmploymentResult is not bool employed || !string.IsNullOrEmpty(result.ErrorType))
        {
            throw new InvalidOperationException("Employment check failure processing is not implemented.");
        }

        await store.RecordCompletionAsync(new EmploymentCheckCompletion(
            request.CorrelationId,
            employed,
            (byte)EmploymentCheckStatus.Completed,
            timeProvider.GetUtcNow().UtcDateTime), cancellationToken);

        await publisher.PublishAsync(new VerifyNotEmployedCompleted
        {
            CorrelationId = request.CorrelationId,
            ULN = request.ULN,
            ApprenticeshipId = request.ApprenticeshipId,
            EmployerAccountIdentifier = request.AccountId,
            NotEmployed = !employed
        }, cancellationToken);
    }
}
