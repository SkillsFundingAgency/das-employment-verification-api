using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Services;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceBusConnection = configuration["PaymentsServiceBusConnection"];
        if (string.IsNullOrWhiteSpace(serviceBusConnection))
        {
            throw new InvalidOperationException("The PaymentsServiceBusConnection setting is required for employment verification publishing.");
        }

        var topicName = configuration["VerifyNotEmployedCompletedTopicName"];
        if (string.IsNullOrWhiteSpace(topicName))
        {
            throw new InvalidOperationException("The VerifyNotEmployedCompletedTopicName setting is required for employment verification publishing.");
        }

        services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));
        services.AddSingleton(provider => provider.GetRequiredService<ServiceBusClient>().CreateSender(topicName));
        services.AddScoped<IEmploymentVerificationPublisher, EmploymentVerificationPublisher>();

        var provisioning = MessagingInfrastructureOptions.Read(configuration);
        if (provisioning.Enabled)
        {
            services.AddSingleton(provisioning);
            services.AddSingleton<IReadOnlyDictionary<ServiceBusNamespace, ServiceBusAdministrationClient>>(_ =>
                new Dictionary<ServiceBusNamespace, ServiceBusAdministrationClient>
                {
                    [ServiceBusNamespace.DasShared] = new ServiceBusAdministrationClient(
                        configuration["DasSharedServiceBusAdministrationConnection"]
                        ?? configuration["DasSharedServiceBusConnection"]),
                    [ServiceBusNamespace.Payments] = new ServiceBusAdministrationClient(
                        configuration["PaymentsServiceBusAdministrationConnection"]
                        ?? configuration["PaymentsServiceBusConnection"])
                });
            services.AddHostedService<SetupMessagingInfrastructure>();
        }

        services.TryAddSingleton(configuration);
        services.TryAddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddHttpClient<IEmploymentCheckClient, EmploymentCheckClient>();

        return services;
    }
}
