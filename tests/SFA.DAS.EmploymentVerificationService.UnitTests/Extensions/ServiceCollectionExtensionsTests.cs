using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Data;
using SFA.DAS.EmploymentVerificationService.Functions.Extensions;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Extensions;

[TestFixture]
[NonParallelizable]
public class ServiceCollectionExtensionsTests
{
    private const string ConnectionString = "Server=localhost;Database=RegistrationTests;Integrated Security=true;TrustServerCertificate=true";

    [Test]
    public void AddEmploymentVerification_RegistersScopedServicesAndSystemClock()
    {
        var services = CreateServices(ConnectionString);
        AssertRegistration<IVerifyNotEmployedService, VerifyNotEmployedService>(services);
        AssertRegistration<IEmploymentCheckCompletionService, EmploymentCheckCompletionService>(services);
        AssertRegistration<IEmploymentCheckStore, EmploymentCheckStore>(services);
        AssertRegistration<IEmploymentCheckClient, MockEmploymentCheckClient>(services);
        AssertRegistration<EmploymentCheckRequestFactory, EmploymentCheckRequestFactory>(services);
        Assert.That(services.Single(x => x.ServiceType == typeof(EmploymentVerificationDbContext)).Lifetime,
            Is.EqualTo(ServiceLifetime.Scoped));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Assert.That(provider.GetRequiredService<TimeProvider>(), Is.SameAs(TimeProvider.System));
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IEmploymentCheckStore>());
    }

    [Test]
    public void AddEmploymentVerification_SharesDependenciesWithinScopeAndSeparatesInvocations()
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
    public void AddEmploymentVerification_RejectsMissingOrBlankSqlSettingDuringRegistration(string? value)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => CreateServices(value));

        Assert.That(failure!.Message, Does.Contain("SqlConnectionString"));
    }

    [Test]
    public void AddEmploymentVerification_AllowsValidSqlSettingDuringRegistration()
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
            .ConfigureServices((_, services) => services.AddEmploymentVerification(configuration));

        var failure = Assert.Throws<InvalidOperationException>(() =>
        {
            using var host = builder.Build();
        });

        Assert.That(failure!.Message, Does.Contain("SqlConnectionString"));
    }

    [TestCase(typeof(IEmploymentCheckCompletionService), typeof(IEmploymentVerificationPublisher))]
    public void AddEmploymentVerification_LeavesUnimplementedDependenciesUnregistered(Type serviceType, Type missingType)
    {
        var services = CreateServices(ConnectionString);
        Assert.That(services.Any(x => x.ServiceType == missingType), Is.False);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var failure = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService(serviceType));

        Assert.That(failure!.Message, Does.Contain(missingType.FullName));
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
                    services.AddSingleton(Mock.Of<IEmploymentVerificationPublisher>());
                    services.AddEmploymentVerification(context.Configuration);
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
            new Dictionary<string, string?> { ["SqlConnectionString"] = connectionString }).Build();
        return new ServiceCollection().AddLogging().AddEmploymentVerification(configuration);
    }

    private static void AssertRegistration<TService, TImplementation>(IServiceCollection services)
    {
        var registration = services.Single(x => x.ServiceType == typeof(TService));
        Assert.That(registration.ImplementationType, Is.EqualTo(typeof(TImplementation)));
        Assert.That(registration.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
    }
}
