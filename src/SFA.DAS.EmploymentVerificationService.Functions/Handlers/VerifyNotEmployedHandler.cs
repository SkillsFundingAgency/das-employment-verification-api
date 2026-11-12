using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public class VerifyNotEmployedHandler(IVerifyNotEmployedService service) : MessageHandler<VerifyNotEmployed>
{
    protected override Task HandleAsync(VerifyNotEmployed message, CancellationToken cancellationToken) =>
        service.ProcessAsync(message, cancellationToken);
}
