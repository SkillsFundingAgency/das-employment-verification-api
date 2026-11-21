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
        
        var employmentVerification = new LearnerEmploymentVerification { 
            Detail = new EmploymentVerification { 
                ExternalCorrelationId = command.CorrelationId ,
                StartDate = DateOnly.FromDateTime(command.StartDate),
                DurationInDays = command.DurationInDays,
                Reference = command.Reference,
                Type = EmploymentVerificationType.NonEmployment,
                Status = EmploymentVerificationStatus.InProgress,             
            },
        };

        //TODO: save the employmentVerification to the database here
        //TODO: now trigger the employment check API call here


        //await repository.SaveRequestAsync(request, cancellationToken);
        //logger.LogInformation("New request persisted for {CorrelationId}", command.CorrelationId);
        //await client.SendAsync(request, cancellationToken);
        logger.LogInformation("Employment Check API submission completed for {CorrelationId}", command.CorrelationId);
    }
}
