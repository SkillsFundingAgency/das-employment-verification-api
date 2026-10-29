using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public sealed class VerifyNotEmployedService(
    IEmploymentCheckStore store,
    EmploymentCheckRequestFactory requestFactory,
    IEmploymentCheckClient client)
{
    public async Task ProcessAsync(VerifyNotEmployed command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await store.ExistsAsync(command.CorrelationId, cancellationToken))
        {
            return;
        }

        var request = requestFactory.Create(command);
        await store.SaveRequestAsync(request, cancellationToken);
        await client.SendAsync(request, cancellationToken);
    }
}
