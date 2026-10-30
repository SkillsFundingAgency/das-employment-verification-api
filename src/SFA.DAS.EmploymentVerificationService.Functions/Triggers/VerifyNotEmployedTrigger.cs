using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Functions.Triggers;

public class VerifyNotEmployedTrigger(IVerifyNotEmployedService service, ILogger<VerifyNotEmployedTrigger> logger)
{
    [Function("VerifyNotEmployed")]
    public async Task Run(
        // Placeholder setting key: the actual queue entity is awaiting design confirmation.
        [ServiceBusTrigger("%VerifyNotEmployedQueueNamePlaceholder%", Connection = "ServiceBusConnection")]
        VerifyNotEmployed command,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["InvocationId"] = context.InvocationId
        });
        logger.LogInformation("VerifyNotEmployed processing started");
        try
        {
            await service.ProcessAsync(command, cancellationToken);
            logger.LogInformation("VerifyNotEmployed processing completed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "VerifyNotEmployed processing failed");
            throw;
        }
    }
}
