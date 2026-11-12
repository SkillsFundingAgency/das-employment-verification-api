using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Processors;

[TestFixture]
public class MessageProcessorFactoryTests
{
    private Mock<IVerifyNotEmployedService> command = null!;
    private Mock<IEmploymentCheckCompletionService> completion = null!;
    private ServiceProvider provider = null!;
    private IServiceScope scope = null!;
    private MessageProcessorFactory factory = null!;

    [SetUp]
    public void SetUp()
    {
        command = new(MockBehavior.Strict);
        completion = new(MockBehavior.Strict);
        provider = new ServiceCollection()
            .AddSingleton(command.Object)
            .AddSingleton(completion.Object)
            .AddScoped<VerifyNotEmployedProcessor>()
            .AddScoped<EmploymentCheckCompletedEventProcessor>()
            .BuildServiceProvider();
        scope = provider.CreateScope();
        factory = new MessageProcessorFactory(scope.ServiceProvider, NullLogger<MessageProcessorFactory>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        scope.Dispose();
        provider.Dispose();
    }

    private static ServiceBusReceivedMessage Message(string? header, string body = "{}") =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(body),
            properties: header is null ? new Dictionary<string, object>() :
                new Dictionary<string, object> { [ServiceBusMessageHeaders.EnclosedMessageTypes] = header });

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessAsync_DispatchesCommandAndPreservesToken(bool assemblyQualified)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var id = Guid.NewGuid();
        command.Setup(x => x.ProcessAsync(It.Is<VerifyNotEmployed>(m => m.CorrelationId == id), cancellation.Token))
            .Returns(Task.CompletedTask);

        // Act
        await factory.ProcessAsync(Message(assemblyQualified ? typeof(VerifyNotEmployed).AssemblyQualifiedName :
            typeof(VerifyNotEmployed).FullName, $"{{\"correlationId\":\"{id}\"}}"), cancellation.Token);

        // Assert
        command.VerifyAll();
        completion.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProcessAsync_DispatchesCompletionWithSemicolonMetadata(bool assemblyQualified)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var id = Guid.NewGuid();
        var date = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expected = new EmploymentCheckResult(id, false, date, "error");
        completion.Setup(x => x.ProcessAsync(expected, cancellation.Token)).Returns(Task.CompletedTask);
        var body = JsonSerializer.Serialize(new EmploymentCheckCompletedEvent(id, false, date, "error"));
        var name = assemblyQualified ? typeof(EmploymentCheckCompletedEvent).AssemblyQualifiedName : typeof(EmploymentCheckCompletedEvent).FullName;

        // Act
        await factory.ProcessAsync(Message($" ; Other.Base, Other;  {name} ; NServiceBus.IEvent, NServiceBus ; ", body), cancellation.Token);

        // Assert
        completion.VerifyAll();
        command.VerifyNoOtherCalls();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Unknown.Message, Unknown")]
    [TestCase("VerifyNotEmployed")]
    [TestCase("sfa.das.employmentverificationservice.messages.learner.commands.verifynotemployed")]
    public void ProcessAsync_RejectsMissingUnsupportedOrInexactMetadata(string? metadata)
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => factory.ProcessAsync(Message(metadata), CancellationToken.None));
        command.VerifyNoOtherCalls();
        completion.VerifyNoOtherCalls();
    }

    [Test]
    public void ProcessAsync_RejectsAmbiguousSupportedTypes() =>
        Assert.ThrowsAsync<InvalidOperationException>(() => factory.ProcessAsync(Message(
            $"{typeof(VerifyNotEmployed).AssemblyQualifiedName};{typeof(EmploymentCheckCompletedEvent).FullName}"), CancellationToken.None));

    [Test]
    public async Task ProcessAsync_AcceptsRepeatedSameConcreteType()
    {
        command.Setup(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None)).Returns(Task.CompletedTask);
        await factory.ProcessAsync(Message($"{typeof(VerifyNotEmployed).FullName};{typeof(VerifyNotEmployed).AssemblyQualifiedName}"), CancellationToken.None);
        command.Verify(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None), Times.Once);
    }

    [Test]
    public void ProcessAsync_RejectsNonStringHeader()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString("{}"),
            properties: new Dictionary<string, object> { [ServiceBusMessageHeaders.EnclosedMessageTypes] = 42 });
        Assert.ThrowsAsync<InvalidOperationException>(() => factory.ProcessAsync(message, CancellationToken.None));
        command.VerifyNoOtherCalls();
    }

    [Test]
    public void ProcessAsync_PropagatesProcessorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        command.Setup(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), cancellation.Token))
            .Callback(() => cancellation.Cancel()).Returns(() => Task.FromCanceled(cancellation.Token));
        Assert.ThrowsAsync<TaskCanceledException>(() => factory.ProcessAsync(Message(typeof(VerifyNotEmployed).FullName), cancellation.Token));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ProcessAsync_PropagatesProcessorFailure(bool synchronous)
    {
        // Arrange
        var failure = new InvalidOperationException("failed");
        var processing = command.Setup(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None));
        if (synchronous)
            processing.Throws(failure);
        else
            processing.ThrowsAsync(failure);

        // Act and Assert
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.ProcessAsync(Message(typeof(VerifyNotEmployed).FullName), CancellationToken.None)), Is.SameAs(failure));
    }

    [Test]
    public async Task ProcessAsync_AwaitsProcessor()
    {
        // Arrange
        var pending = new TaskCompletionSource();
        command.Setup(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None)).Returns(pending.Task);

        // Act
        var processing = factory.ProcessAsync(Message(typeof(VerifyNotEmployed).FullName), CancellationToken.None);

        // Assert
        Assert.That(processing.IsCompleted, Is.False);
        pending.SetResult();
        await processing;
    }

    [Test]
    public void ProcessAsync_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            factory.ProcessAsync(Message(typeof(VerifyNotEmployed).FullName), cancellation.Token));
        command.VerifyNoOtherCalls();
    }
}
