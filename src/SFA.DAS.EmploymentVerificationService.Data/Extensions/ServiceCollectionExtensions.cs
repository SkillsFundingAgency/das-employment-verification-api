using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;

namespace SFA.DAS.EmploymentVerificationService.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["SqlConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The SqlConnectionString setting is required for employment verification persistence.");
        }

        services.AddScoped<IEmploymentCheckRepository, EmploymentCheckRepository>();
        services.AddDbContext<EmploymentVerificationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        return services;
    }
}
