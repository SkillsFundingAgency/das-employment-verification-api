using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Data;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Clients;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SFA.DAS.EmploymentVerificationService.Specs.StepDefinitions;

public sealed class TestSession
{
    private HttpClient? httpClient;

    public WireMockServer Api { get; private set; } = null!;
    public VerifyNotEmployedService VerifyNotEmployedService { get; private set; } = null!;
    public EmploymentCheckCompletionService EmploymentCheckCompletionService { get; private set; } = null!;
    public EmploymentVerificationDbContext DataContext { get; private set; } = null!;
    public List<ServiceBusMessage> PublishedMessages { get; } = [];
    public VerifyNotEmployed Command { get; } = new()
    {
        CorrelationId = Guid.NewGuid(),
        ULN = 1234567890,
        EmployerAccountIdentifier = 456,
        ApprenticeshipId = 789,
        Reference = "PV2-4341 acceptance scenario",
        StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DurationInDays = 30
    };

    public void Start(IConfiguration configuration)
    {
        Api = WireMockServer.Start();
        Api.Given(Request.Create().WithPath("/api/EmploymentCheck/RegisterCheck").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmploymentCheckApiUrl"] = Api.Url + "/api/EmploymentCheck/RegisterCheck"
        }).Build();

        var sender = new Mock<ServiceBusSender>(MockBehavior.Strict);
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((message, _) => PublishedMessages.Add(message))
            .Returns(Task.CompletedTask);

        var options = new DbContextOptionsBuilder<EmploymentVerificationDbContext>()
            .UseSqlServer(configuration.GetConnectionString("EmploymentVerificationConnectionString"))
            .Options;
        DataContext = new EmploymentVerificationDbContext(options);
        var repository = new EmploymentCheckRepository(DataContext);
        var requestFactory = new EmploymentCheckRequestFactory(TimeProvider.System);
        httpClient = new HttpClient();
        var client = new EmploymentCheckClient(httpClient, settings,
            Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == Environments.Development),
            NullLogger<EmploymentCheckClient>.Instance, new DefaultAzureCredential());
        var publisher = new EmploymentVerificationPublisher(sender.Object);
        VerifyNotEmployedService = new VerifyNotEmployedService(repository, requestFactory, client,
            NullLogger<VerifyNotEmployedService>.Instance);
        EmploymentCheckCompletionService = new EmploymentCheckCompletionService(repository, publisher,
            TimeProvider.System, NullLogger<EmploymentCheckCompletionService>.Instance);
    }

    public void Stop()
    {
        try
        {
            DataContext?.Dispose();
        }
        finally
        {
            try { httpClient?.Dispose(); }
            finally { Api?.Dispose(); }
        }
    }
}
