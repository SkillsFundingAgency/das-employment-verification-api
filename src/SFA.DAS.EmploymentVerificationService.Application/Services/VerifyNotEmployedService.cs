using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public sealed class VerifyNotEmployedService(
    IEmploymentCheckStore store,
    EmploymentCheckRequestFactory requestFactory,
    IEmploymentCheckClient client,
    ILogger<VerifyNotEmployedService> logger) : IVerifyNotEmployedService
{
    public async Task ProcessAsync(VerifyNotEmployed command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await store.ExistsAsync(command.CorrelationId, cancellationToken))
        {
            logger.LogInformation("Existing request ignored for {CorrelationId}", command.CorrelationId);
            return;
        }

        var request = requestFactory.Create(command);
        await store.SaveRequestAsync(request, cancellationToken);
        logger.LogInformation("New request persisted for {CorrelationId}", command.CorrelationId);
        await client.SendAsync(request, cancellationToken);
        logger.LogInformation("Employment Check API submission completed for {CorrelationId}", command.CorrelationId);
    }
}
