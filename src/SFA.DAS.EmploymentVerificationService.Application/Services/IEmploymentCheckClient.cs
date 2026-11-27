using SFA.DAS.EmploymentVerificationService.Application.Models;

namespace SFA.DAS.EmploymentVerificationService.Application.Services;

public interface IEmploymentCheckClient
{
    Task SendAsync(EmploymentCheckRequest request, CancellationToken cancellationToken);
}
