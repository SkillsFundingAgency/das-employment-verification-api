using System.Text.Json;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Functions.Handlers;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Handlers;

[TestFixture]
public class MessageHandlerTests
{
    [Test]
    public async Task HandleAsync_DeserializesCaseInsensitivePropertiesAndForwardsToken()
    {
        using var source = new CancellationTokenSource();
        var handler = new RecordingHandler();

        await ((IMessageHandler)handler).HandleAsync(new BinaryData("{\"vAlUe\":\"example\"}"), source.Token);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Message?.Value, Is.EqualTo("example"));
            Assert.That(handler.Token, Is.EqualTo(source.Token));
        });
    }

    [TestCase("null")]
    [TestCase("{")]
    [TestCase("")]
    [TestCase("[]")]
    public void HandleAsync_RejectsInvalidBodyWithoutCallingTypedHandler(string json)
    {
        var handler = new RecordingHandler();

        Assert.ThrowsAsync<JsonException>(() => handler.HandleAsync(new BinaryData(json), CancellationToken.None));

        Assert.That(handler.Message, Is.Null);
    }

    [Test]
    public void HandleAsync_RejectsCancelledTokenBeforeDeserializing()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var handler = new RecordingHandler();

        var exception = Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new BinaryData("{"), source.Token));

        Assert.That(exception!.CancellationToken, Is.EqualTo(source.Token));
        Assert.That(handler.Message, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HandleAsync_PreservesProcessingException(bool synchronous)
    {
        var failure = new InvalidOperationException("Processing failed");
        var handler = new RecordingHandler { Failure = failure, ThrowSynchronously = synchronous };

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new BinaryData("{}"), CancellationToken.None));

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public void HandleAsync_PreservesProcessingCancellation()
    {
        using var source = new CancellationTokenSource();
        var handler = new RecordingHandler { Result = Task.FromCanceled(new CancellationToken(true)) };

        Assert.ThrowsAsync<TaskCanceledException>(() =>
            handler.HandleAsync(new BinaryData("{}"), source.Token));

        Assert.That(handler.Token, Is.EqualTo(source.Token));
    }

    public sealed class TestMessage
    {
        public string? Value { get; set; }
    }

    private sealed class RecordingHandler : MessageHandler<TestMessage>
    {
        public TestMessage? Message { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; init; }
        public bool ThrowSynchronously { get; init; }
        public Task Result { get; init; } = Task.CompletedTask;

        protected override Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
        {
            Message = message;
            Token = cancellationToken;
            if (Failure is not null)
            {
                if (ThrowSynchronously)
                    throw Failure;
                return Task.FromException(Failure);
            }

            return Result;
        }
    }
}
