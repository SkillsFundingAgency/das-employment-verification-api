using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Functions.Triggers;

public class EmploymentCheckCompletedTrigger(IEmploymentCheckCompletionService service, ILogger<EmploymentCheckCompletedTrigger> logger)
{
    [Function("EmploymentCheckCompleted")]
    public async Task Run(
        [ServiceBusTrigger("%EmploymentCheckCompletedQueueName%", Connection = "ServiceBusConnection")]
        EmploymentCheckCompletedEvent message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("EmploymentCheckCompleted processing started for {CorrelationId}", message.CorrelationId);
        try
        {
            await service.ProcessAsync(new EmploymentCheckResult(
                message.CorrelationId,
                message.EmploymentResult,
                message.CheckDate,
                message.ErrorType), cancellationToken);
            logger.LogInformation("EmploymentCheckCompleted processing completed for {CorrelationId}", message.CorrelationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "EmploymentCheckCompleted processing failed for {CorrelationId}", message.CorrelationId);
            throw;
        }
    }
}
