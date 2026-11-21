using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Messaging;

[TestFixture]
public class ForwardToValidationTests
{
    private const string QueueName = "sfa-das-evs-verify-not-employed";
    private static readonly string SqlFilter = MessagingInfrastructureOptions.SqlFilter;
    private Mock<ServiceBusAdministrationClient> client = null!;
    private SubscriptionProperties subscription = null!;
    private MessagingInfrastructureOptions options = null!;
    private SetupMessagingInfrastructure service = null!;

    [SetUp]
    public void SetUp()
    {
        client = new Mock<ServiceBusAdministrationClient>(MockBehavior.Strict);
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
            ServiceBusModelFactory.RuleProperties(MessagingInfrastructureOptions.RuleName, new SqlRuleFilter(SqlFilter), null),
            ServiceBusModelFactory.RuleProperties("$Default", new TrueRuleFilter(), null)
        };
        client.Setup(x => x.GetRulesAsync("bundle-1", QueueName, CancellationToken.None))
            .Returns(AsyncPageable<RuleProperties>.FromPages(
                [Page<RuleProperties>.FromValues(rules, null, Mock.Of<Response>())]));
        client.Setup(x => x.DeleteRuleAsync("bundle-1", QueueName, "$Default", CancellationToken.None))
            .ReturnsAsync(Mock.Of<Response>());
        options = new MessagingInfrastructureOptions
        {
            Enabled = true,
            TopicName = "bundle-1",
            SubscriptionName = QueueName,
            QueueName = QueueName
        };
        service = new SetupMessagingInfrastructure(client.Object, options,
            NullLogger<SetupMessagingInfrastructure>.Instance);
    }

    [TestCase(QueueName)]
    [TestCase("https://das-pv2-dev-sa.servicebus.windows.net/" + QueueName)]
    [TestCase("SFA-DAS-EVS-VERIFY-NOT-EMPLOYED")]
    public async Task StartAsync_AcceptsEquivalentDestinationsAndRemovesDefaultAfterValidation(string forwardTo)
    {
        subscription.ForwardTo = forwardTo;

        await service.StartAsync(CancellationToken.None);

        client.Verify(x => x.DeleteRuleAsync("bundle-1", QueueName, "$Default", CancellationToken.None), Times.Once);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("wrong-queue")]
    public void StartAsync_RejectsIncorrectDestinationsWithoutRemovingDefault(string? forwardTo)
    {
        subscription.ForwardTo = forwardTo;

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("configured Service Bus namespace"));
        client.Verify(x => x.DeleteRuleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task StartAsync_CreatesOneForwardingSubscriptionWithOnlyCombinedRule()
    {
        // Arrange
        client.Setup(x => x.QueueExistsAsync(QueueName, CancellationToken.None))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
        client.Setup(x => x.CreateQueueAsync(It.Is<CreateQueueOptions>(q => q.Name == QueueName), CancellationToken.None))
            .ReturnsAsync(Response.FromValue<QueueProperties>(null!, Mock.Of<Response>()));
        client.Setup(x => x.SubscriptionExistsAsync("bundle-1", QueueName, CancellationToken.None))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
        client.Setup(x => x.CreateSubscriptionAsync(
                It.Is<CreateSubscriptionOptions>(o => o.TopicName == "bundle-1" && o.SubscriptionName == QueueName && o.ForwardTo == QueueName),
                It.Is<CreateRuleOptions>(r => r.Name == MessagingInfrastructureOptions.RuleName &&
                    r.Filter is SqlRuleFilter && ((SqlRuleFilter)r.Filter).SqlExpression == SqlFilter), CancellationToken.None))
            .ReturnsAsync(Response.FromValue(subscription, Mock.Of<Response>())).Verifiable();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        client.Verify();
        client.Verify(x => x.GetRulesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("missing")]
    [TestCase("wrong-filter")]
    [TestCase("unexpected")]
    public void StartAsync_RejectsIncompatibleExistingRulesWithoutDeletingResources(string problem)
    {
        // Arrange
        var rules = problem switch
        {
            "missing" => Array.Empty<RuleProperties>(),
            "wrong-filter" => new[] { ServiceBusModelFactory.RuleProperties(MessagingInfrastructureOptions.RuleName, new TrueRuleFilter(), null) },
            _ => new[] { ServiceBusModelFactory.RuleProperties("unexpected", new TrueRuleFilter(), null) }
        };
        client.Setup(x => x.GetRulesAsync("bundle-1", QueueName, CancellationToken.None))
            .Returns(AsyncPageable<RuleProperties>.FromPages(
                [Page<RuleProperties>.FromValues(rules, null, Mock.Of<Response>())]));

        // Act and Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
        client.Verify(x => x.DeleteRuleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
