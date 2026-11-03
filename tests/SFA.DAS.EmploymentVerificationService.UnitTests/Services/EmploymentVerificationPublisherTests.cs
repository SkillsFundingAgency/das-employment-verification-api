using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Services;

[TestFixture]
public class EmploymentVerificationPublisherTests
{
    [TestCase(null)]
    [TestCase(123L)]
    public async Task PublishAsync_SendsContractAndMetadataWithCancellation(long? apprenticeshipId)
    {
        var message = new VerifyNotEmployedCompleted
        {
            CorrelationId = Guid.NewGuid(),
            ULN = 1234567890,
            ApprenticeshipId = apprenticeshipId,
            EmployerAccountIdentifier = 456,
            NotEmployed = true
        };
        using var cancellation = new CancellationTokenSource();
        ServiceBusMessage? sent = null;
        var sender = new Mock<ServiceBusSender>();
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), cancellation.Token))
            .Callback<ServiceBusMessage, CancellationToken>((value, _) => sent = value)
            .Returns(Task.CompletedTask);

        await new EmploymentVerificationPublisher(sender.Object).PublishAsync(message, cancellation.Token);

        sender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), cancellation.Token), Times.Once);
        Assert.That(sent, Is.Not.Null);
        Assert.That(sent!.ContentType, Is.EqualTo("application/json"));
        Assert.That(sent.Subject, Is.EqualTo("VerifyNotEmployedCompleted"));
        Assert.That(sent.CorrelationId, Is.EqualTo(message.CorrelationId.ToString()));
        Assert.That(sent.ApplicationProperties, Is.Empty);
        using var body = JsonDocument.Parse(sent.Body.ToString());
        var root = body.RootElement;
        Assert.That(root.EnumerateObject().Count(), Is.EqualTo(5));
        Assert.That(root.GetProperty("CorrelationId").GetGuid(), Is.EqualTo(message.CorrelationId));
        Assert.That(root.GetProperty("ULN").GetInt64(), Is.EqualTo(message.ULN));
        Assert.That(root.GetProperty("EmployerAccountIdentifier").GetInt64(), Is.EqualTo(message.EmployerAccountIdentifier));
        Assert.That(root.GetProperty("NotEmployed").GetBoolean(), Is.True);
        Assert.That(root.GetProperty("ApprenticeshipId").Deserialize<long?>(), Is.EqualTo(apprenticeshipId));
    }

    [Test]
    public void PublishAsync_PropagatesSendException()
    {
        var failure = new ServiceBusException("Send failed", ServiceBusFailureReason.ServiceCommunicationProblem);
        var sender = new Mock<ServiceBusSender>();
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var actual = Assert.ThrowsAsync<ServiceBusException>(() =>
            new EmploymentVerificationPublisher(sender.Object).PublishAsync(new VerifyNotEmployedCompleted(), CancellationToken.None));

        Assert.That(actual, Is.SameAs(failure));
    }

    [Test]
    public void PublishAsync_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sender = new Mock<ServiceBusSender>();
        sender.Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), cancellation.Token))
            .Returns(Task.FromCanceled(cancellation.Token));

        Assert.ThrowsAsync<TaskCanceledException>(() =>
            new EmploymentVerificationPublisher(sender.Object).PublishAsync(new VerifyNotEmployedCompleted(), cancellation.Token));
    }
}
