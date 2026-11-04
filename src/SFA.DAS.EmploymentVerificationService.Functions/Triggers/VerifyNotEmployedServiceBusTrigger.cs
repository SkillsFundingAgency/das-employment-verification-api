using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Functions.Triggers;

public class VerifyNotEmployedServiceBusTrigger(IVerifyNotEmployedService service, ILogger<VerifyNotEmployedServiceBusTrigger> logger)
{
    [Function(nameof(VerifyNotEmployedServiceBusTrigger))]
    public async Task Run(
        [ServiceBusTrigger("%VerifyNotEmployedQueueName%", Connection = "ServiceBusConnection")]
        VerifyNotEmployed command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("VerifyNotEmployed processing started for {CorrelationId}", command.CorrelationId);

        try
        {
            await service.ProcessAsync(command, cancellationToken);
            logger.LogInformation("VerifyNotEmployed processing completed for {CorrelationId}", command.CorrelationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "VerifyNotEmployed processing failed for {CorrelationId}", command.CorrelationId);
            throw;
        }
    }
}
//das-
