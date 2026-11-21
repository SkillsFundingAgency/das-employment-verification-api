using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Services;

namespace SFA.DAS.EmploymentVerificationService.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<EmploymentCheckRequestFactory>();
        services.AddScoped<IVerifyNotEmployedService, VerifyNotEmployedService>();
        services.AddScoped<IEmploymentCheckCompletionService, EmploymentCheckCompletionService>();

        return services;
    }
}
