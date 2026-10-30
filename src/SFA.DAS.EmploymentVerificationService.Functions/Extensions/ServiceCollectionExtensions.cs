using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Data;

namespace SFA.DAS.EmploymentVerificationService.Functions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmploymentVerification(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Functions exposes local.settings.json Values and deployed app settings as environment variables.
        var connectionString = configuration["SqlConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The SqlConnectionString setting is required for employment verification persistence.");
        }

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<EmploymentCheckRequestFactory>();
        services.AddScoped<IVerifyNotEmployedService, VerifyNotEmployedService>();
        services.AddScoped<IEmploymentCheckCompletionService, EmploymentCheckCompletionService>();
        services.AddScoped<IEmploymentCheckStore, EmploymentCheckStore>();
        services.AddDbContext<EmploymentVerificationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        // IEmploymentCheckClient and IEmploymentVerificationPublisher await concrete implementations.
        return services;
    }
}
