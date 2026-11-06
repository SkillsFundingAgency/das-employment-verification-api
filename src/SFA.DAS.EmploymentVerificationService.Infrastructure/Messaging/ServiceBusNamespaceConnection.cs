using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

public static class ServiceBusNamespaceConnection
{
    public static string AdministrationConnection(IConfiguration configuration, ServiceBusNamespace serviceBusNamespace)
    {
        var prefix = serviceBusNamespace switch
        {
            ServiceBusNamespace.DasShared => "DasSharedServiceBus",
            ServiceBusNamespace.Payments => "PaymentsServiceBus",
            _ => throw new InvalidOperationException("Unknown Service Bus namespace.")
        };
        var runtime = configuration[prefix + "Connection"];
        if (string.IsNullOrWhiteSpace(runtime))
            throw new InvalidOperationException($"The {prefix}Connection setting is required for messaging provisioning.");

        var administration = configuration[prefix + "AdministrationConnection"];
        if (string.IsNullOrWhiteSpace(administration))
            return runtime;

        var runtimeProperties = ServiceBusConnectionStringProperties.Parse(runtime);
        var administrationProperties = ServiceBusConnectionStringProperties.Parse(administration);
        if (!string.Equals(runtimeProperties.FullyQualifiedNamespace, administrationProperties.FullyQualifiedNamespace,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{prefix}AdministrationConnection must use the same namespace as {prefix}Connection.");

        return administration;
    }
}
