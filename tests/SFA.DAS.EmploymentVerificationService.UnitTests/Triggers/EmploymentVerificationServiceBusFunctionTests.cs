using System.Reflection;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Infrastructure.Messaging;
using SFA.DAS.EmploymentVerificationService.Functions.Handlers;
using SFA.DAS.EmploymentVerificationService.Functions.Triggers;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Triggers;

public class EmploymentVerificationServiceBusFunctionTests
{
    private Mock<IMessageHandler<VerifyNotEmployed>> command = null!;
    private Mock<IMessageHandler<EmploymentCheckCompletedEvent>> completion = null!;
    private ServiceProvider provider = null!;
    private IServiceScope scope = null!;
    private IMessageHandlerFactory factory = null!;
    private EmploymentVerificationServiceBusFunction function = null!;
    private ScopeCapturingLoggerProvider scopes = null!;

    [SetUp]
    public void SetUp()
    {
        command = new(MockBehavior.Strict);
        completion = new(MockBehavior.Strict);
        scopes = new ScopeCapturingLoggerProvider();
        provider = new ServiceCollection().AddLogging(logging => logging.AddProvider(scopes))
            .AddScoped<IMessageHandler<VerifyNotEmployed>>(_ => command.Object)
            .AddScoped<IMessageHandler<EmploymentCheckCompletedEvent>>(_ => completion.Object)
            .AddScoped<IMessageHandlerFactory, MessageHandlerFactory>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        scope = provider.CreateScope();
        factory = scope.ServiceProvider.GetRequiredService<IMessageHandlerFactory>();
        function = new EmploymentVerificationServiceBusFunction(factory);
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
    public async Task Run_DispatchesCommandAndPreservesToken(bool assemblyQualified)
    {
        using var cancellation = new CancellationTokenSource();
        var id = Guid.NewGuid();
        command.Setup(x => x.HandleAsync(It.Is<VerifyNotEmployed>(m => m.CorrelationId == id), cancellation.Token))
            .Returns(Task.CompletedTask);
        await function.Run(Message(assemblyQualified ? typeof(VerifyNotEmployed).AssemblyQualifiedName :
            typeof(VerifyNotEmployed).FullName, $"{{\"correlationId\":\"{id}\"}}"), cancellation.Token);
        command.VerifyAll();
        completion.VerifyNoOtherCalls();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Run_DispatchesCompletionWithSemicolonMetadata(bool assemblyQualified)
    {
        using var cancellation = new CancellationTokenSource();
        var id = Guid.NewGuid();
        var date = DateTime.UtcNow;
        completion.Setup(x => x.HandleAsync(It.Is<EmploymentCheckCompletedEvent>(m =>
            m.CorrelationId == id && m.EmploymentResult == false && m.CheckDate == date && m.ErrorType == "error"),
            cancellation.Token)).Returns(Task.CompletedTask);
        var body = JsonSerializer.Serialize(new EmploymentCheckCompletedEvent(id, false, date, "error"));
        var name = assemblyQualified ? typeof(EmploymentCheckCompletedEvent).AssemblyQualifiedName : typeof(EmploymentCheckCompletedEvent).FullName;
        await function.Run(Message($" ; Other.Base, Other;  {name} ; NServiceBus.IEvent, NServiceBus ; ", body), cancellation.Token);
        completion.VerifyAll();
        command.VerifyNoOtherCalls();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Unknown.Message, Unknown")]
    [TestCase("VerifyNotEmployed")]
    [TestCase("SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands.VerifyNotEmployedExtra")]
    [TestCase("Prefix.SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands.VerifyNotEmployed")]
    [TestCase("sfa.das.employmentverificationservice.messages.learner.commands.verifynotemployed")]
    public void Run_RejectsMissingUnsupportedOrInexactMetadata(string? metadata)
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => function.Run(Message(metadata), CancellationToken.None));
        command.VerifyNoOtherCalls();
        completion.VerifyNoOtherCalls();
    }

    [Test]
    public void Factory_RejectsAmbiguousSupportedTypes() =>
        Assert.ThrowsAsync<InvalidOperationException>(() => factory.HandleAsync(Message(
            $"{typeof(VerifyNotEmployed).AssemblyQualifiedName};{typeof(EmploymentCheckCompletedEvent).FullName}"), CancellationToken.None));

    [Test]
    public async Task Factory_AcceptsRepeatedSameConcreteType()
    {
        command.Setup(x => x.HandleAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None)).Returns(Task.CompletedTask);
        await factory.HandleAsync(Message($"{typeof(VerifyNotEmployed).FullName};{typeof(VerifyNotEmployed).AssemblyQualifiedName}"), CancellationToken.None);
        command.Verify(x => x.HandleAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None), Times.Once);
    }

    [TestCase("{")]
    [TestCase("null")]
    [TestCase("{\"CorrelationId\":\"invalid\"}")]
    public void Run_RejectsInvalidJson(string body)
    {
        Assert.ThrowsAsync<JsonException>(() => function.Run(Message(typeof(VerifyNotEmployed).FullName, body), CancellationToken.None));
        command.VerifyNoOtherCalls();
    }

    [Test]
    public void Run_RejectsNonStringHeader()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString("{}"),
            properties: new Dictionary<string, object> { [ServiceBusMessageHeaders.EnclosedMessageTypes] = 42 });
        Assert.ThrowsAsync<InvalidOperationException>(() => function.Run(message, CancellationToken.None));
        command.VerifyNoOtherCalls();
    }

    [Test]
    public void Run_PropagatesHandlerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        command.Setup(x => x.HandleAsync(It.IsAny<VerifyNotEmployed>(), cancellation.Token))
            .Callback(() => cancellation.Cancel()).Returns(() => Task.FromCanceled(cancellation.Token));
        Assert.ThrowsAsync<TaskCanceledException>(() => function.Run(Message(typeof(VerifyNotEmployed).FullName), cancellation.Token));
    }

    [Test]
    public void Run_PropagatesHandlerFailure()
    {
        var failure = new InvalidOperationException("failed");
        command.Setup(x => x.HandleAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None)).ThrowsAsync(failure);
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(() =>
            function.Run(Message(typeof(VerifyNotEmployed).FullName), CancellationToken.None)), Is.SameAs(failure));
    }

    [Test]
    public async Task Run_AwaitsHandler()
    {
        var pending = new TaskCompletionSource();
        command.Setup(x => x.HandleAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None)).Returns(pending.Task);
        var processing = function.Run(Message(typeof(VerifyNotEmployed).FullName), CancellationToken.None);
        Assert.That(processing.IsCompleted, Is.False);
        pending.SetResult();
        await processing;
    }

    [Test]
    public void Run_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() =>
            function.Run(Message(typeof(VerifyNotEmployed).FullName), cancellation.Token));
        command.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Run_InvokesHandlerWithinMessageLoggingScope()
    {
        IReadOnlyDictionary<string, object?>? active = null;
        command.Setup(x => x.HandleAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None))
            .Callback(() => active = scopes.ActiveScopeValues())
            .Returns(Task.CompletedTask);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(BinaryData.FromString("{}"),
            messageId: "message-1", deliveryCount: 3,
            properties: new Dictionary<string, object> { [ServiceBusMessageHeaders.EnclosedMessageTypes] = typeof(VerifyNotEmployed).FullName! });

        await function.Run(message, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(active, Is.Not.Null);
            Assert.That(active!["MessageId"], Is.EqualTo("message-1"));
            Assert.That(active["DeliveryCount"], Is.EqualTo(3));
            Assert.That(scopes.ActiveScopeValues(), Is.Empty, "Scope must end when processing ends.");
        });
    }

    [Test]
    public void Run_EndsLoggingScopeWhenProcessingFails()
    {
        command.Setup(x => x.HandleAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("failed"));

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            function.Run(Message(typeof(VerifyNotEmployed).FullName), CancellationToken.None));
        Assert.That(scopes.ActiveScopeValues(), Is.Empty);
    }

    [Test]
    public void Run_UsesSingleDasSharedQueueWithAutomaticCompletion()
    {
        var binding = typeof(EmploymentVerificationServiceBusFunction).GetMethod("Run")!.GetParameters()[0]
            .GetCustomAttribute<ServiceBusTriggerAttribute>()!;
        Assert.Multiple(() =>
        {
            Assert.That(binding.QueueName, Is.EqualTo("%EvsQueueName%"));
            Assert.That(binding.Connection, Is.EqualTo("DasSharedServiceBusConnection"));
            Assert.That(binding.AutoCompleteMessages, Is.True);
        });
    }

    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => this.scopeProvider = scopeProvider;

        public IReadOnlyDictionary<string, object?> ActiveScopeValues()
        {
            var values = new Dictionary<string, object?>();
            scopeProvider.ForEachScope((scope, state) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    foreach (var (key, value) in pairs)
                        state[key] = value;
            }, values);
            return values;
        }

        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;

        public void Dispose() { }
    }
}
