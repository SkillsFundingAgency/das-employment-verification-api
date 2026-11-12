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
    private EmploymentVerificationServiceBusTrigger trigger = null!;

    [SetUp]
    public void SetUp()
    {
        factory = new Mock<IMessageProcessorFactory>();
        trigger = new EmploymentVerificationServiceBusTrigger(factory.Object);
    }

    [Test]
    public async Task Run_ForwardsMessageAndCancellationToken()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage();
        using var cancellation = new CancellationTokenSource();
        factory.Setup(x => x.ProcessAsync(message, cancellation.Token)).Returns(Task.CompletedTask);

        await trigger.Run(message, cancellation.Token);

        factory.Verify(x => x.ProcessAsync(message, cancellation.Token), Times.Once);
    }

    [Test]
    public void Run_PropagatesProcessingException()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage();
        var failure = new InvalidOperationException("Processing failed");
        factory.Setup(x => x.ProcessAsync(message, CancellationToken.None)).ThrowsAsync(failure);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => trigger.Run(message, CancellationToken.None));

        Assert.That(exception, Is.SameAs(failure));
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
