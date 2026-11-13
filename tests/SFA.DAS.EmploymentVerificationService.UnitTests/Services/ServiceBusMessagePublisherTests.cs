using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Extensions;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Services;

[TestFixture]
public class ServiceBusMessagePublisherTests
{
    private Mock<ServiceBusSender> sender = null!;
    private ServiceBusMessagePublisher publisher = null!;

    [SetUp]
    public void SetUp()
    {
        sender = new Mock<ServiceBusSender>();
        publisher = new ServiceBusMessagePublisher(sender.Object);
    }

    [TestCase(null, "bundle-1")]
    [TestCase("completion-topic", "completion-topic")]
    public async Task PublisherDestination_UsesDasSharedAndConfiguredTopic(string? outgoingTopic, string expectedTopic)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DasSharedServiceBusConnection"] = "Endpoint=sb://das-shared.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA==",
            ["DasSharedServiceBusTopicName"] = "bundle-1",
            ["VerifyNotEmployedCompletedTopicName"] = outgoingTopic
        }).Build();
        await using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();

        // Act
        var destination = provider.GetRequiredService<ServiceBusSender>();
        using var scope = provider.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<IMessagePublisher>(), Is.TypeOf<ServiceBusMessagePublisher>());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(destination.FullyQualifiedNamespace, Is.EqualTo("das-shared.servicebus.windows.net"));
            Assert.That(destination.EntityPath, Is.EqualTo(expectedTopic));
        });
    }

    [TestCase(null, true)]
    [TestCase(123L, false)]
    public async Task PublishAsync_SendsContractAndMetadataWithCancellation(long? apprenticeshipId, bool notEmployed)
    {
        // Arrange
        var message = new VerifyNotEmployedCompleted
        {
            CorrelationId = Guid.NewGuid(),
            ULN = 1234567890,
            ApprenticeshipId = apprenticeshipId,
            EmployerAccountIdentifier = 456,
            NotEmployed = notEmployed
        };
        using var cancellation = new CancellationTokenSource();
        ServiceBusMessage? sent = null;
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), cancellation.Token))
            .Callback<ServiceBusMessage, CancellationToken>((value, _) => sent = value)
            .Returns(Task.CompletedTask);

        // Act
        await publisher.PublishAsync(message, cancellation.Token);

        // Assert
        sender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), cancellation.Token), Times.Once);
        Assert.That(sent, Is.Not.Null);
        Assert.That(sent!.ContentType, Is.EqualTo("application/json"));
        Assert.That(sent.Subject, Is.Null);
        Assert.That(sent.CorrelationId, Is.Null);
        Assert.That(sent.ApplicationProperties, Has.Count.EqualTo(1));
        Assert.That(sent.ApplicationProperties["NServiceBus.EnclosedMessageTypes"],
            Is.EqualTo(typeof(VerifyNotEmployedCompleted).AssemblyQualifiedName));
        using var body = JsonDocument.Parse(sent.Body.ToString());
        var root = body.RootElement;
        Assert.That(root.EnumerateObject().Count(), Is.EqualTo(5));
        Assert.That(root.GetProperty("CorrelationId").GetGuid(), Is.EqualTo(message.CorrelationId));
        Assert.That(root.GetProperty("ULN").GetInt64(), Is.EqualTo(message.ULN));
        Assert.That(root.GetProperty("EmployerAccountIdentifier").GetInt64(), Is.EqualTo(message.EmployerAccountIdentifier));
        Assert.That(root.GetProperty("NotEmployed").GetBoolean(), Is.EqualTo(notEmployed));
        Assert.That(root.GetProperty("ApprenticeshipId").Deserialize<long?>(), Is.EqualTo(apprenticeshipId));
    }

    [Test]
    public async Task PublishAsync_SupportsAnotherMessageType()
    {
        var message = new TestMessage("Example");
        ServiceBusMessage? sent = null;
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None))
            .Callback<ServiceBusMessage, CancellationToken>((value, _) => sent = value)
            .Returns(Task.CompletedTask);

        await publisher.PublishAsync(message, CancellationToken.None);

        Assert.That(sent, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(sent!.Body.ToString(), Is.EqualTo(JsonSerializer.Serialize(message)));
            Assert.That(sent.ContentType, Is.EqualTo("application/json"));
            Assert.That(sent.Subject, Is.Null);
            Assert.That(sent.CorrelationId, Is.Null);
            Assert.That(sent.ApplicationProperties["NServiceBus.EnclosedMessageTypes"],
                Is.EqualTo(typeof(TestMessage).AssemblyQualifiedName));
        });
    }

    private sealed record TestMessage(string Value);

    [Test]
    public void PublishAsync_PropagatesSendException()
    {
        // Arrange
        var failure = new ServiceBusException("Send failed", ServiceBusFailureReason.ServiceCommunicationProblem);
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        // Act and Assert
        var actual = Assert.ThrowsAsync<ServiceBusException>(() =>
            publisher.PublishAsync(new VerifyNotEmployedCompleted(), CancellationToken.None));

        Assert.That(actual, Is.SameAs(failure));
    }

    [Test]
    public void PublishAsync_PropagatesCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), cancellation.Token))
            .Returns(Task.FromCanceled(cancellation.Token));

        // Act and Assert
        Assert.ThrowsAsync<TaskCanceledException>(() =>
            publisher.PublishAsync(new VerifyNotEmployedCompleted(), cancellation.Token));
    }
}
