using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Functions.Processors;

public class VerifyNotEmployedProcessor(IVerifyNotEmployedService service) : MessageProcessor<VerifyNotEmployed>
{
    protected override Task ProcessAsync(VerifyNotEmployed message, CancellationToken cancellationToken) =>
        service.ProcessAsync(message, cancellationToken);
}
