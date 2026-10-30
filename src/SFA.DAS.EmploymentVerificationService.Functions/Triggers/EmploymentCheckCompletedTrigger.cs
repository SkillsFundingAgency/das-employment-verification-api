using Microsoft.Azure.Functions.Worker;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Functions.Triggers;

public class EmploymentCheckCompletedTrigger(IEmploymentCheckCompletionService service)
{
    [Function("EmploymentCheckCompleted")]
    public Task Run(
        // Placeholder setting key: the entity and subscription arrangement await design confirmation.
        [ServiceBusTrigger("%EmploymentCheckCompletedQueueNamePlaceholder%", Connection = "ServiceBusConnection")]
        EmploymentCheckCompletedEvent message,
        CancellationToken cancellationToken)
    {
        return service.ProcessAsync(new EmploymentCheckResult(
            message.CorrelationId,
            message.EmploymentResult,
            message.CheckDate,
            message.ErrorType), cancellationToken);
    }
}
