using Microsoft.Azure.Functions.Worker;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Functions.Triggers;

public class VerifyNotEmployedTrigger(IVerifyNotEmployedService service)
{
    [Function("VerifyNotEmployed")]
    public Task Run(
        // Placeholder setting key: the actual queue entity is awaiting design confirmation.
        [ServiceBusTrigger("%VerifyNotEmployedQueueNamePlaceholder%", Connection = "ServiceBusConnection")]
        VerifyNotEmployed command,
        CancellationToken cancellationToken)
    {
        return service.ProcessAsync(command, cancellationToken);
    }
}
