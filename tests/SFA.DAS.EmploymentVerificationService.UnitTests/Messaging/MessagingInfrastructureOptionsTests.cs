using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Messaging;

[TestFixture]
public class MessagingInfrastructureOptionsTests
{
    private IConfiguration configuration = null!;

    [SetUp]
    public void SetUp()
    {
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceBusSetupEnabled"] = "true",
            ["ServiceBusFiltersReviewed"] = "true",
            ["PaymentsServiceBusTopicName"] = "payments-topic",
            ["DasSharedServiceBusTopicName"] = "das-topic",
            ["VerifyNotEmployedQueueName"] = "command-queue",
            ["VerifyNotEmployedSubscriptionName"] = "command-subscription",
            ["EmploymentCheckCompletedQueueName"] = "completion-queue",
            ["EmploymentCheckCompletedSubscriptionName"] = "completion-subscription"
        }).Build();
    }

    [TestCase(null)]
    [TestCase("false")]
    public void Read_WhenDisabled_DoesNotRequireRoutesOrFilterReview(string? enabled)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceBusSetupEnabled"] = enabled
        }).Build();

        var options = MessagingInfrastructureOptions.Read(settings);

        Assert.Multiple(() =>
        {
            Assert.That(options.Enabled, Is.False);
            Assert.That(options.Routes, Is.Empty);
        });
    }

    [TestCase(null)]
    [TestCase("false")]
    public void Read_WhenFiltersAreNotReviewed_RejectsProvisioning(string? reviewed)
    {
        configuration["ServiceBusFiltersReviewed"] = reviewed;

        var exception = Assert.Throws<InvalidOperationException>(() => MessagingInfrastructureOptions.Read(configuration));

        Assert.That(exception!.Message, Does.Contain("ServiceBusFiltersReviewed"));
    }

    [TestCase("PaymentsServiceBusTopicName")]
    [TestCase("DasSharedServiceBusTopicName")]
    [TestCase("VerifyNotEmployedQueueName")]
    [TestCase("VerifyNotEmployedSubscriptionName")]
    [TestCase("EmploymentCheckCompletedQueueName")]
    [TestCase("EmploymentCheckCompletedSubscriptionName")]
    public void Read_WhenRequiredSettingIsMissing_IdentifiesTheSetting(string settingName)
    {
        configuration[settingName] = null;

        var exception = Assert.Throws<InvalidOperationException>(() => MessagingInfrastructureOptions.Read(configuration));

        Assert.That(exception!.Message, Does.Contain(settingName));
    }

    [Test]
    public void For_ConstructsBothRoutesWithConfiguredResourceNames()
    {
        var routes = EvsRoutes.For(configuration);

        Assert.Multiple(() =>
        {
            Assert.That(routes, Has.Count.EqualTo(2));
            Assert.That(routes[0].RuleName, Is.EqualTo("VerifyNotEmployed"));
            Assert.That(routes[1].RuleName, Is.EqualTo("EmploymentCheckCompletedEvent"));
            Assert.That(routes.Select(route => route.TopicName), Is.EqualTo(new[] { "payments-topic", "das-topic" }));
            Assert.That(routes[0].Namespace, Is.EqualTo(ServiceBusNamespace.Payments));
            Assert.That(routes[1].Namespace, Is.EqualTo(ServiceBusNamespace.DasShared));
            Assert.That(routes[0].QueueName, Is.EqualTo("command-queue"));
            Assert.That(routes[0].SubscriptionName, Is.EqualTo("command-subscription"));
            Assert.That(routes[1].QueueName, Is.EqualTo("completion-queue"));
            Assert.That(routes[1].SubscriptionName, Is.EqualTo("completion-subscription"));
        });
    }

    [Test]
    public void For_UsesVerifiedEmploymentCheckFilterAndLocalTestingOnlyDataLocksFilter()
    {
        var routes = EvsRoutes.For(configuration);

        Assert.Multiple(() =>
        {
            // LOCAL TESTING ONLY: this does not establish the real DataLocks publisher metadata.
            Assert.That(routes[0].SqlFilter, Is.EqualTo("sys.Label = 'VerifyNotEmployed'"));
            Assert.That(routes[1].SqlFilter, Is.EqualTo(
                "[NServiceBus.EnclosedMessageTypes] LIKE '%SFA.DAS.EmploymentCheck.Types.EmploymentCheckCompletedEvent%'"));
        });
    }

    [Test]
    public void Read_WithLocalTestingOnlyFilterAndReviewedRoutes_ReturnsEnabledOptions()
    {
        var options = MessagingInfrastructureOptions.Read(configuration);

        Assert.Multiple(() =>
        {
            Assert.That(options.Enabled, Is.True);
            Assert.That(options.Routes, Has.Count.EqualTo(2));
        });
    }

    [TestCase("VerifyNotEmployedQueueName", "EmploymentCheckCompletedQueueName")]
    [TestCase("VerifyNotEmployedSubscriptionName", "EmploymentCheckCompletedSubscriptionName")]
    public void Read_WhenRoutesShareNamesAcrossNamespaces_AllowsProvisioning(string firstSetting, string secondSetting)
    {
        configuration[secondSetting] = configuration[firstSetting]!.ToUpperInvariant();

        configuration["DasSharedServiceBusTopicName"] = configuration["PaymentsServiceBusTopicName"];
        Assert.That(MessagingInfrastructureOptions.Read(configuration).Enabled, Is.True);
    }
    [TestCase(true)]
    [TestCase(false)]
    public void ValidateRoutes_RejectsDuplicateNamesWithinNamespace(bool duplicateQueue)
    {
        var routes = EvsRoutes.For(configuration);
        routes[1].Namespace = routes[0].Namespace;
        if (duplicateQueue)
            routes[1].QueueName = routes[0].QueueName.ToUpperInvariant();
        else
        {
            routes[1].TopicName = routes[0].TopicName;
            routes[1].SubscriptionName = routes[0].SubscriptionName.ToUpperInvariant();
        }
        Assert.That(Assert.Throws<InvalidOperationException>(() => MessagingInfrastructureOptions.ValidateRoutes(routes))!.Message,
            Does.Contain("within each namespace"));
    }
}
