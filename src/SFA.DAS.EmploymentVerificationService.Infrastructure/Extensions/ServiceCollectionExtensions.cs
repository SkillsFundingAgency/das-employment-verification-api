using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Services;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceBusConnection = configuration["ServiceBusConnection"];
        if (string.IsNullOrWhiteSpace(serviceBusConnection))
        {
            throw new InvalidOperationException("The ServiceBusConnection setting is required for employment verification publishing.");
        }

        var entityName = configuration["VerifyNotEmployedCompletedEntityName"];
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new InvalidOperationException("The VerifyNotEmployedCompletedEntityName setting is required for employment verification publishing.");
        }

        services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));
        services.AddSingleton(provider => provider.GetRequiredService<ServiceBusClient>().CreateSender(entityName));
        services.AddScoped<IEmploymentVerificationPublisher, EmploymentVerificationPublisher>();

        // TODO: Replace with the real EmploymentCheckClient before deployment.
        services.AddScoped<IEmploymentCheckClient, MockEmploymentCheckClient>();

        return services;
    }
}
