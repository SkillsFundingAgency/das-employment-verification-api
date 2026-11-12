using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Functions.Handlers;

public class EmploymentCheckCompletedEventHandler(IEmploymentCheckCompletionService service)
    : IMessageHandler<EmploymentCheckCompletedEvent>
{
    public Task HandleAsync(EmploymentCheckCompletedEvent message, CancellationToken cancellationToken) =>
        service.ProcessAsync(new EmploymentCheckResult(
            message.CorrelationId, message.EmploymentResult, message.CheckDate, message.ErrorType), cancellationToken);
}
