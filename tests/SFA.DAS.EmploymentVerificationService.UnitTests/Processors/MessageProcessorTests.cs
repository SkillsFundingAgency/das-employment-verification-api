using System.Text.Json;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Processors;

[TestFixture]
public class MessageProcessorTests
{
    [Test]
    public async Task ProcessAsync_DeserializesCaseInsensitivePropertiesAndForwardsToken()
    {
        using var source = new CancellationTokenSource();
        var processor = new RecordingProcessor();

        await ((IMessageProcessor)processor).ProcessAsync(new BinaryData("{\"vAlUe\":\"example\"}"), source.Token);

        Assert.Multiple(() =>
        {
            Assert.That(processor.Message?.Value, Is.EqualTo("example"));
            Assert.That(processor.Token, Is.EqualTo(source.Token));
        });
    }

    [TestCase("null")]
    [TestCase("{")]
    [TestCase("")]
    [TestCase("[]")]
    public void ProcessAsync_RejectsInvalidBodyWithoutCallingTypedProcessor(string json)
    {
        var processor = new RecordingProcessor();

        Assert.ThrowsAsync<JsonException>(() => processor.ProcessAsync(new BinaryData(json), CancellationToken.None));

        Assert.That(processor.Message, Is.Null);
    }

    [Test]
    public void ProcessAsync_RejectsCancelledTokenBeforeDeserializing()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var processor = new RecordingProcessor();

        var exception = Assert.ThrowsAsync<OperationCanceledException>(() =>
            processor.ProcessAsync(new BinaryData("{"), source.Token));

        Assert.That(exception!.CancellationToken, Is.EqualTo(source.Token));
        Assert.That(processor.Message, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ProcessAsync_PreservesProcessingException(bool synchronous)
    {
        var failure = new InvalidOperationException("Processing failed");
        var processor = new RecordingProcessor { Failure = failure, ThrowSynchronously = synchronous };

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(new BinaryData("{}"), CancellationToken.None));

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public void ProcessAsync_PreservesProcessingCancellation()
    {
        using var source = new CancellationTokenSource();
        var processor = new RecordingProcessor { Result = Task.FromCanceled(new CancellationToken(true)) };

        Assert.ThrowsAsync<TaskCanceledException>(() =>
            processor.ProcessAsync(new BinaryData("{}"), source.Token));

        Assert.That(processor.Token, Is.EqualTo(source.Token));
    }

    public sealed class TestMessage
    {
        public string? Value { get; set; }
    }

    private sealed class RecordingProcessor : MessageProcessor<TestMessage>
    {
        public TestMessage? Message { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; init; }
        public bool ThrowSynchronously { get; init; }
        public Task Result { get; init; } = Task.CompletedTask;

        protected override Task ProcessAsync(TestMessage message, CancellationToken cancellationToken)
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
