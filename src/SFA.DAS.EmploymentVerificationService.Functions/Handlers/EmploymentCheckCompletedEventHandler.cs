using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public class EmploymentCheckCompletedEventHandler(IEmploymentCheckCompletionService service)
    : MessageHandler<EmploymentCheckCompletedEvent>
{
    protected override Task HandleAsync(EmploymentCheckCompletedEvent message, CancellationToken cancellationToken) =>
        service.ProcessAsync(new EmploymentCheckResult(
            message.CorrelationId, message.EmploymentResult, message.CheckDate, message.ErrorType), cancellationToken);
}
