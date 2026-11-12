using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Functions.Processors;

public class EmploymentCheckCompletedEventProcessor(IEmploymentCheckCompletionService service)
    : MessageProcessor<EmploymentCheckCompletedEvent>
{
    protected override Task ProcessAsync(EmploymentCheckCompletedEvent message, CancellationToken cancellationToken) =>
        service.ProcessAsync(new EmploymentCheckResult(
            message.CorrelationId, message.EmploymentResult, message.CheckDate, message.ErrorType), cancellationToken);
}
