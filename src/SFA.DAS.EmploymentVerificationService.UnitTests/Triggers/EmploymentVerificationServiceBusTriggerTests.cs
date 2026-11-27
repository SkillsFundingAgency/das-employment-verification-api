using System.Reflection;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;
using SFA.DAS.EmploymentVerificationService.Functions.Triggers;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Triggers;

[TestFixture]
public class EmploymentVerificationServiceBusTriggerTests
{
    private Mock<IMessageProcessorFactory> factory = null!;
    private Mock<IMessageProcessor> processor = null!;
    private ServiceBusReceivedMessage message = null!;
    private EmploymentVerificationServiceBusTrigger trigger = null!;

    [SetUp]
    public void SetUp()
    {
        message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{\"value\":42}"));
        processor = new Mock<IMessageProcessor>(MockBehavior.Strict);
        factory = new Mock<IMessageProcessorFactory>(MockBehavior.Strict);
        factory.Setup(x => x.GetProcessor(message)).Returns(processor.Object);
        trigger = new EmploymentVerificationServiceBusTrigger(factory.Object);
    }

    [Test]
    public async Task Run_ForwardsBodyAndCancellationTokenAndReturnsProcessorTask()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource();
        processor.Setup(x => x.ProcessAsync(It.Is<BinaryData>(body => body.ToString() == message.Body.ToString()), cancellation.Token)).Returns(pending.Task);

        // Act
        var processing = trigger.Run(message, cancellation.Token);

        // Assert
        Assert.That(processing, Is.SameAs(pending.Task));
        factory.Verify(x => x.GetProcessor(message), Times.Once);
        processor.Verify(x => x.ProcessAsync(It.Is<BinaryData>(body => body.ToString() == message.Body.ToString()), cancellation.Token), Times.Once);
        pending.SetResult();
        await processing;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Run_PropagatesProcessingException(bool synchronous)
    {
        // Arrange
        var failure = new InvalidOperationException("Processing failed");
        var processing = processor.Setup(x => x.ProcessAsync(It.Is<BinaryData>(body => body.ToString() == message.Body.ToString()), CancellationToken.None));
        if (synchronous)
            processing.Throws(failure);
        else
            processing.ThrowsAsync(failure);

        // Act and Assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => trigger.Run(message, CancellationToken.None));
        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public void Run_PropagatesFactoryException()
    {
        // Arrange
        var failure = new InvalidOperationException("Invalid metadata");
        factory.Setup(x => x.GetProcessor(message)).Throws(failure);

        // Act and Assert
        var exception = Assert.Throws<InvalidOperationException>(() => trigger.Run(message, CancellationToken.None));
        Assert.That(exception, Is.SameAs(failure));
        processor.VerifyNoOtherCalls();
    }

    [Test]
    public void Run_PropagatesProcessorCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        processor.Setup(x => x.ProcessAsync(It.Is<BinaryData>(body => body.ToString() == message.Body.ToString()), cancellation.Token))
            .Returns(Task.FromCanceled(cancellation.Token));

        // Act and Assert
        var exception = Assert.ThrowsAsync<TaskCanceledException>(() => trigger.Run(message, cancellation.Token));
        Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
    }

    [Test]
    public void Run_UsesSingleDasSharedQueueWithAutomaticCompletion()
    {
        var binding = typeof(EmploymentVerificationServiceBusTrigger).GetMethod("Run")!.GetParameters()[0]
            .GetCustomAttribute<ServiceBusTriggerAttribute>()!;
        Assert.Multiple(() =>
        {
            Assert.That(binding.QueueName, Is.EqualTo("%EvsQueueName%"));
            Assert.That(binding.Connection, Is.EqualTo("DasSharedServiceBusConnection"));
            Assert.That(binding.AutoCompleteMessages, Is.True);
        });
    }
}
