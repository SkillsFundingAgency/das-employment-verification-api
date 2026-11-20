using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;
using SFA.DAS.EmploymentVerificationService.Models;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public sealed class VerifyNotEmployedService(
    IEmploymentCheckRepository repository,
    EmploymentCheckRequestFactory requestFactory,
    IEmploymentCheckClient client,
    ILogger<VerifyNotEmployedService> logger) : IVerifyNotEmployedService
{
    public async Task ProcessAsync(VerifyNotEmployed command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await repository.VerificationExists(command.CorrelationId, cancellationToken))
        {
            logger.LogInformation("Existing request ignored for {CorrelationId}", command.CorrelationId);
            return;
        }

        var durationInDays = command.DurationInDays == 0 ? 30 : command.DurationInDays;
        var request = requestFactory.Create(command, Guid.NewGuid(), durationInDays);
        var startDate = DateOnly.FromDateTime(request.StartDate);
        var verification = new LearnerEmploymentVerification
        {
            Detail = new EmploymentVerification
            {
                ExternalCorrelationId = command.CorrelationId,
                StartDate = startDate,
                DurationInDays = durationInDays,
                Reference = command.Reference,
                Type = EmploymentVerificationType.NonEmployment,
                Status = EmploymentVerificationStatus.InProgress
            },
            UniqueLearnerNumber = command.ULN,
            EmployerAccountId = command.EmployerAccountIdentifier,
            ApprenticeshipId = command.ApprenticeshipId,
            LearnerEmploymentChecks =
            [
                new LearnerEmploymentCheck
                {
                    UniqueLearnerNumber = command.ULN,
                    EmployerAccountId = command.EmployerAccountIdentifier,
                    Detail = new EmploymentCheck
                    {
                        CheckCorrelationId = request.CorrelationId,
                        StartDate = startDate,
                        EndDate = DateOnly.FromDateTime(request.EndDate),
                        Reference = request.Reference,
                        RequestTime = request.CreatedOn,
                        Status = request.Status
                    }
                }
            ]
        };

        await repository.SaveVerificationAsync(verification, cancellationToken);
        logger.LogInformation("New request persisted for {CorrelationId}", command.CorrelationId);
        await client.SendAsync(request, cancellationToken);
        logger.LogInformation("Employment Check API submission completed for {CorrelationId}", command.CorrelationId);
    }
}
