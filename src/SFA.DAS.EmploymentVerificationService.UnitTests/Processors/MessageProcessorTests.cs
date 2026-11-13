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
        // Arrange
        using var source = new CancellationTokenSource();
        var processor = new RecordingProcessor();

        // Act
        await ((IMessageProcessor)processor).ProcessAsync(new BinaryData("{\"vAlUe\":\"example\"}"), source.Token);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(processor.Message?.Value, Is.EqualTo("example"));
            Assert.That(processor.Token, Is.EqualTo(source.Token));
        });
    }

    [TestCase("null")]
    [TestCase("{")]
    public void ProcessAsync_RejectsInvalidBodyWithoutCallingTypedProcessor(string json)
    {
        var processor = new RecordingProcessor();

        Assert.ThrowsAsync<JsonException>(() => processor.ProcessAsync(new BinaryData(json), CancellationToken.None));

        Assert.That(processor.Message, Is.Null);
    }

    [Test]
    public void ProcessAsync_RejectsCancelledTokenBeforeDeserializing()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var processor = new RecordingProcessor();

        // Act and Assert
        var exception = Assert.ThrowsAsync<OperationCanceledException>(() =>
            processor.ProcessAsync(new BinaryData("{"), source.Token));

        Assert.That(exception!.CancellationToken, Is.EqualTo(source.Token));
        Assert.That(processor.Message, Is.Null);
    }

    public sealed class TestMessage
    {
        public string? Value { get; set; }
    }

    private sealed class RecordingProcessor : MessageProcessor<TestMessage>
    {
        public TestMessage? Message { get; private set; }
        public CancellationToken Token { get; private set; }

        protected override Task ProcessAsync(TestMessage message, CancellationToken cancellationToken)
        {
            Message = message;
            Token = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
