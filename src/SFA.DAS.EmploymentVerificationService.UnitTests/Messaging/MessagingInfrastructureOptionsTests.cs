using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Messaging;

public class MessagingInfrastructureOptionsTests
{
    [Test]
    public void Read_UsesSingleConfiguredRouteAndCombinedFilter()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ServiceBusSetupEnabled"] = "true", ["EvsQueueName"] = "evs",
            ["EvsSubscriptionName"] = "evs-subscription"
        }).Build();

        // Act
        var options = MessagingInfrastructureOptions.Read(configuration);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(options.TopicName, Is.EqualTo("bundle-1"));
            Assert.That(options.QueueName, Is.EqualTo("evs"));
            Assert.That(options.SubscriptionName, Is.EqualTo("evs-subscription"));
            Assert.That(MessagingInfrastructureOptions.SqlFilter, Is.EqualTo(
                $"[NServiceBus.EnclosedMessageTypes] LIKE '%{typeof(VerifyNotEmployed).FullName}%' OR " +
                $"[NServiceBus.EnclosedMessageTypes] LIKE '%{typeof(EmploymentCheckCompletedEvent).FullName}%'"));
        });
    }

    [TestCase("EvsQueueName")]
    [TestCase("EvsSubscriptionName")]
    public void Read_RejectsMissingRequiredSetting(string setting)
    {
        var values = new Dictionary<string, string?> { ["ServiceBusSetupEnabled"] = "true",
            ["EvsQueueName"] = "evs", ["EvsSubscriptionName"] = "evs" };

        values.Remove(setting);

        Assert.Throws<InvalidOperationException>(() => MessagingInfrastructureOptions.Read(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }
}
