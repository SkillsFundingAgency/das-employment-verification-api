using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Messaging.ServiceBus;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Data;
using SFA.DAS.EmploymentVerificationService.Application.Extensions;
using SFA.DAS.EmploymentVerificationService.Data.Extensions;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Extensions;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Services;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Extensions;

[TestFixture]
[NonParallelizable]
public class ServiceCollectionExtensionsTests
{
    private const string BusConnection = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dGVzdA==;UseDevelopmentEmulator=true;";
    private const string ConnectionString = "Server=localhost;Database=RegistrationTests;Integrated Security=true;TrustServerCertificate=true";

    [Test]
    public void Registrations_RegistersScopedServicesAndSystemClock()
    {
        var services = CreateServices(ConnectionString);
        AssertRegistration<IVerifyNotEmployedService, VerifyNotEmployedService>(services);
        AssertRegistration<IEmploymentCheckCompletionService, EmploymentCheckCompletionService>(services);
        AssertRegistration<IEmploymentCheckStore, EmploymentCheckStore>(services);
        AssertRegistration<IEmploymentVerificationPublisher, EmploymentVerificationPublisher>(services);
        Assert.That(services.Single(x => x.ServiceType == typeof(ServiceBusClient)).Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        Assert.That(services.Single(x => x.ServiceType == typeof(ServiceBusSender)).Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
        AssertRegistration<IEmploymentCheckClient, MockEmploymentCheckClient>(services);
        AssertRegistration<EmploymentCheckRequestFactory, EmploymentCheckRequestFactory>(services);
        Assert.That(services.Single(x => x.ServiceType == typeof(EmploymentVerificationDbContext)).Lifetime,
            Is.EqualTo(ServiceLifetime.Scoped));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Assert.That(provider.GetRequiredService<TimeProvider>(), Is.SameAs(TimeProvider.System));
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IEmploymentCheckStore>());
    }

    [Test]
    public void Registrations_SharesDependenciesWithinScopeAndSeparatesInvocations()
    {
        using var provider = CreateServices(ConnectionString)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var context = first.ServiceProvider.GetRequiredService<EmploymentVerificationDbContext>();
        var store = first.ServiceProvider.GetRequiredService<IEmploymentCheckStore>();

        Assert.That(context.Database.ProviderName, Is.EqualTo("Microsoft.EntityFrameworkCore.SqlServer"));
        AssertConnectionString(context);
        Assert.That(context, Is.SameAs(first.ServiceProvider.GetRequiredService<EmploymentVerificationDbContext>()));
        Assert.That(context, Is.Not.SameAs(second.ServiceProvider.GetRequiredService<EmploymentVerificationDbContext>()));
        Assert.That(store, Is.TypeOf<EmploymentCheckStore>());
        Assert.That(store, Is.SameAs(first.ServiceProvider.GetRequiredService<IEmploymentCheckStore>()));
        Assert.That(store, Is.Not.SameAs(second.ServiceProvider.GetRequiredService<IEmploymentCheckStore>()));
        Assert.That(first.ServiceProvider.GetRequiredService<EmploymentCheckRequestFactory>(), Is.Not.Null);
        var client = first.ServiceProvider.GetRequiredService<IEmploymentCheckClient>();
        Assert.That(client, Is.TypeOf<MockEmploymentCheckClient>());
        Assert.That(client, Is.SameAs(first.ServiceProvider.GetRequiredService<IEmploymentCheckClient>()));
        Assert.That(client, Is.Not.SameAs(second.ServiceProvider.GetRequiredService<IEmploymentCheckClient>()));
        Assert.That(first.ServiceProvider.GetRequiredService<IVerifyNotEmployedService>(), Is.TypeOf<VerifyNotEmployedService>());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\t\r\n")]
    public void Registrations_RejectsMissingOrBlankSqlSettingDuringRegistration(string? value)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => CreateServices(value));

        Assert.That(failure!.Message, Does.Contain("SqlConnectionString"));
    }

    [Test]
    public void Registrations_AllowsValidSqlSettingDuringRegistration()
    {
        Assert.DoesNotThrow(() => CreateServices(ConnectionString));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("\t ")]
    public void WorkerConfiguration_RejectsMissingOrBlankSqlSettingDuringHostBuild(string? value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            value is null ? new Dictionary<string, string?>() :
                new Dictionary<string, string?> { ["SqlConnectionString"] = value }).Build();
        var builder = new HostBuilder()
            .UseEnvironment(Environments.Development)
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices((_, services) => services.AddApplication().AddData(configuration).AddInfrastructure(configuration));

        var failure = Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = builder.Build();
        });

        Assert.That(failure!.Message, Does.Contain("SqlConnectionString"));
    }

    [Test]
    public void WorkerConfiguration_ReadsSqlSettingFromEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            Environment.SetEnvironmentVariable("SqlConnectionString", ConnectionString);
            using var host = new HostBuilder()
                .UseEnvironment(Environments.Development)
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices((context, services) =>
                {
                    services.AddApplication().AddData(context.Configuration).AddInfrastructure(new ConfigurationBuilder()
                        .AddInMemoryCollection(PublisherSettings()).AddConfiguration(context.Configuration).Build());
                })
                .Build();
            using var scope = host.Services.CreateScope();

            AssertConnectionString(scope.ServiceProvider.GetRequiredService<EmploymentVerificationDbContext>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SqlConnectionString", previous);
        }
    }

    [TestCase("ServiceBusConnection", null)]
    [TestCase("ServiceBusConnection", "")]
    [TestCase("ServiceBusConnection", " \t")]
    [TestCase("VerifyNotEmployedCompletedEntityName", null)]
    [TestCase("VerifyNotEmployedCompletedEntityName", "")]
    [TestCase("VerifyNotEmployedCompletedEntityName", " \t")]
    public void Registrations_RejectsMissingPublisherSettings(string key, string? value)
    {
        var settings = PublisherSettings();
        settings[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var failure = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration));
        Assert.That(failure!.Message, Does.Contain(key));
    }

    [Test]
    public async Task Registrations_ReusesBusResourcesAcrossPublisherScopes()
    {
        await using var provider = CreateServices(ConnectionString).BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var publisher = first.ServiceProvider.GetRequiredService<IEmploymentVerificationPublisher>();
        Assert.That(publisher, Is.TypeOf<EmploymentVerificationPublisher>());
        Assert.That(publisher, Is.SameAs(first.ServiceProvider.GetRequiredService<IEmploymentVerificationPublisher>()));
        Assert.That(publisher, Is.Not.SameAs(second.ServiceProvider.GetRequiredService<IEmploymentVerificationPublisher>()));
        Assert.That(first.ServiceProvider.GetRequiredService<ServiceBusSender>(),
            Is.SameAs(second.ServiceProvider.GetRequiredService<ServiceBusSender>()));
        Assert.That(first.ServiceProvider.GetRequiredService<ServiceBusClient>(),
            Is.SameAs(second.ServiceProvider.GetRequiredService<ServiceBusClient>()));
        Assert.That(first.ServiceProvider.GetRequiredService<IEmploymentCheckCompletionService>(), Is.Not.Null);
    }

    private static Dictionary<string, string?> PublisherSettings(string? sqlConnection = ConnectionString) => new()
    {
        ["SqlConnectionString"] = sqlConnection,
        ["ServiceBusConnection"] = BusConnection,
        ["VerifyNotEmployedCompletedEntityName"] = "registration-tests"
    };

    private static void AssertConnectionString(EmploymentVerificationDbContext context)
    {
        // The SQL Server provider normalizes aliases and may append provider defaults.
        var actual = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        var expected = new SqlConnectionStringBuilder(ConnectionString);
        Assert.That(actual.DataSource, Is.EqualTo(expected.DataSource));
        Assert.That(actual.InitialCatalog, Is.EqualTo(expected.InitialCatalog));
        Assert.That(actual.IntegratedSecurity, Is.EqualTo(expected.IntegratedSecurity));
        Assert.That(actual.TrustServerCertificate, Is.EqualTo(expected.TrustServerCertificate));
    }

    private static IServiceCollection CreateServices(string? connectionString)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            PublisherSettings(connectionString)).Build();
        return new ServiceCollection().AddLogging().AddApplication().AddData(configuration).AddInfrastructure(configuration);
    }

    private static void AssertRegistration<TService, TImplementation>(IServiceCollection services)
    {
        var registration = services.Single(x => x.ServiceType == typeof(TService));
        Assert.That(registration.ImplementationType, Is.EqualTo(typeof(TImplementation)));
        Assert.That(registration.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
    }
}
