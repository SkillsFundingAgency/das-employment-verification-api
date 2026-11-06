using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Messaging;

[TestFixture]
public class ForwardToValidationTests
{
    private const string QueueName = "sfa-das-evs-verify-not-employed";
    private const string Namespace = "das-pv2-dev-sa.servicebus.windows.net";
    private const string SqlFilter = "sys.Label = 'test-command'";
    private Mock<ServiceBusAdministrationClient> client = null!;
    private IConfiguration configuration = null!;
    private SubscriptionProperties subscription = null!;
    private MessagingInfrastructureOptions options = null!;
    private SetupMessagingInfrastructure service = null!;

    [SetUp]
    public void SetUp()
    {
        client = new Mock<ServiceBusAdministrationClient>(MockBehavior.Strict);
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceBusConnection"] = $"Endpoint=sb://{Namespace}/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA=="
        }).Build();
        subscription = ServiceBusModelFactory.SubscriptionProperties("bundle-1", QueueName,
            lockDuration: TimeSpan.FromMinutes(5), defaultMessageTimeToLive: TimeSpan.FromDays(7),
            autoDeleteOnIdle: TimeSpan.MaxValue, maxDeliveryCount: 50, status: EntityStatus.Active,
            forwardTo: QueueName, userMetadata: string.Empty);
        client.Setup(x => x.TopicExistsAsync("bundle-1", CancellationToken.None))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        client.Setup(x => x.QueueExistsAsync(QueueName, CancellationToken.None))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        client.Setup(x => x.SubscriptionExistsAsync("bundle-1", QueueName, CancellationToken.None))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        client.Setup(x => x.GetSubscriptionAsync("bundle-1", QueueName, CancellationToken.None))
            .ReturnsAsync(() => Response.FromValue(subscription, Mock.Of<Response>()));
        var rules = new[]
        {
            ServiceBusModelFactory.RuleProperties("VerifyNotEmployed", new SqlRuleFilter(SqlFilter), null),
            ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter(), null)
        };
        client.Setup(x => x.GetRulesAsync("bundle-1", QueueName, CancellationToken.None))
            .Returns(AsyncPageable<RuleProperties>.FromPages(
                [Page<RuleProperties>.FromValues(rules, null, Mock.Of<Response>())]));
        client.Setup(x => x.DeleteRuleAsync("bundle-1", QueueName, "$Default", CancellationToken.None))
            .ReturnsAsync(Mock.Of<Response>());
        options = new MessagingInfrastructureOptions(true,
        [
            new MessagingRoute
            {
                RuleName = "VerifyNotEmployed",
                TopicName = "bundle-1",
                SubscriptionName = QueueName,
                QueueName = QueueName,
                SqlFilter = SqlFilter
            }
        ]);
        service = new SetupMessagingInfrastructure(client.Object, options, configuration,
            NullLogger<SetupMessagingInfrastructure>.Instance);
    }

    [TestCase(QueueName)]
    [TestCase("http://" + Namespace + "/" + QueueName)]
    [TestCase("https://" + Namespace + "/" + QueueName)]
    [TestCase("sb://" + Namespace + "/" + QueueName)]
    [TestCase("http://DAS-PV2-DEV-SA.servicebus.windows.net/SFA-DAS-EVS-VERIFY-NOT-EMPLOYED")]
    public async Task StartAsync_AcceptsEquivalentDestinationsAndRemovesDefaultAfterValidation(string forwardTo)
    {
        subscription.ForwardTo = forwardTo;

        await service.StartAsync(CancellationToken.None);

        client.Verify(x => x.DeleteRuleAsync("bundle-1", QueueName, "$Default", CancellationToken.None), Times.Once);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("wrong-queue")]
    [TestCase("http://" + Namespace + "/wrong-queue")]
    [TestCase("http://other.servicebus.windows.net/" + QueueName)]
    [TestCase("http://" + Namespace + ".example.com/" + QueueName)]
    [TestCase("http://" + Namespace + "/prefix/" + QueueName)]
    [TestCase("http://" + Namespace + ":9999/" + QueueName)]
    [TestCase("http://" + Namespace + "/" + QueueName + "#other")]
    public void StartAsync_RejectsIncorrectDestinationsWithoutRemovingDefault(string? forwardTo)
    {
        subscription.ForwardTo = forwardTo;

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("configured Service Bus namespace"));
        client.Verify(x => x.DeleteRuleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("")]
    [TestCase(" \t")]
    public void StartAsync_WhenSqlFilterIsEmpty_RejectsEnabledProvisioningWithoutRemovingDefault(string sqlFilter)
    {
        options.Routes[0].SqlFilter = sqlFilter;

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("SQL filter"));
        client.Verify(x => x.DeleteRuleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("admin.servicebus.windows.net", true)]
    [TestCase(Namespace, false)]
    public async Task StartAsync_ValidatesAgainstAdministrationConnectionNamespace(string destinationNamespace, bool expected)
    {
        configuration["ServiceBusAdministrationConnection"] =
            "Endpoint=sb://admin.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA==";
        subscription.ForwardTo = $"http://{destinationNamespace}/{QueueName}";

        if (expected)
            await service.StartAsync(CancellationToken.None);
        else
            Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

        client.Verify(x => x.DeleteRuleAsync("bundle-1", QueueName, "$Default", CancellationToken.None),
            expected ? Times.Once() : Times.Never());
    }
}
