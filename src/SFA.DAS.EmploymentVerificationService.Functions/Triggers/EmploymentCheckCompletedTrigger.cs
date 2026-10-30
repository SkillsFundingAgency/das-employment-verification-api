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
        // Placeholder setting key: the entity and subscription arrangement await design confirmation.
        [ServiceBusTrigger("%EmploymentCheckCompletedQueueNamePlaceholder%", Connection = "ServiceBusConnection")]
        EmploymentCheckCompletedEvent message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["InvocationId"] = context.InvocationId
        });
        logger.LogInformation("EmploymentCheckCompleted processing started");
        try
        {
            await service.ProcessAsync(new EmploymentCheckResult(
                message.CorrelationId,
                message.EmploymentResult,
                message.CheckDate,
                message.ErrorType), cancellationToken);
            logger.LogInformation("EmploymentCheckCompleted processing completed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "EmploymentCheckCompleted processing failed");
            throw;
        }
    }
}
