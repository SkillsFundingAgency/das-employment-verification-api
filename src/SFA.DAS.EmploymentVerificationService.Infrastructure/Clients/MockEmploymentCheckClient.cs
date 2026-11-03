using Microsoft.Extensions.Logging;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;

public sealed class MockEmploymentCheckClient(ILogger<MockEmploymentCheckClient> logger) : IEmploymentCheckClient
{
    public Task SendAsync(EmploymentCheckRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation("Mock Employment Check API accepted request for {CorrelationId}", request.CorrelationId);
        return Task.CompletedTask;
    }
}
