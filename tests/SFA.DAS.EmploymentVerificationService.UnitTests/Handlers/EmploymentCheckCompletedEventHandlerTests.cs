using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Handlers;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Handlers;

[TestFixture]
public class EmploymentCheckCompletedEventHandlerTests
{
    private Mock<IEmploymentCheckCompletionService> service = null!;
    private EmploymentCheckCompletedEventHandler handler = null!;

    [SetUp]
    public void SetUp()
    {
        service = new Mock<IEmploymentCheckCompletionService>(MockBehavior.Strict);
        handler = new EmploymentCheckCompletedEventHandler(service.Object);
    }

    [TestCase("true", "", false)]
    [TestCase("false", "", false)]
    [TestCase("null", "CheckFailed", false)]
    [TestCase("null", "CheckFailed", true)]
    public async Task HandleAsync_MapsDeserializedEvent(
        string resultJson, string errorType, bool camelCase)
    {
        var correlationId = Guid.NewGuid();
        var checkDate = new DateTime(2026, 10, 30, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234567);
        var json = $$"""
            {"CorrelationId":"{{correlationId}}","EmploymentResult":{{resultJson}},"CheckDate":"{{checkDate:O}}","ErrorType":"{{errorType}}"}
            """;
        if (camelCase)
        {
            json = json.Replace("CorrelationId", "correlationId")
                .Replace("EmploymentResult", "employmentResult")
                .Replace("CheckDate", "checkDate")
                .Replace("ErrorType", "errorType");
        }

        var serializer = new JsonObjectSerializer(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var message = (EmploymentCheckCompletedEvent)(await serializer.DeserializeAsync(
            body, typeof(EmploymentCheckCompletedEvent), CancellationToken.None))!;

        bool? employmentResult = resultJson == "null" ? null : bool.Parse(resultJson);
        var expected = new EmploymentCheckResult(correlationId, employmentResult, checkDate, errorType);
        service.Setup(x => x.ProcessAsync(expected, CancellationToken.None)).Returns(Task.CompletedTask);

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.That(message.CheckDate.Kind, Is.EqualTo(DateTimeKind.Utc));
        service.Verify(x => x.ProcessAsync(expected, CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Test]
    public async Task HandleAsync_ForwardsCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var message = new EmploymentCheckCompletedEvent(Guid.NewGuid(), false, DateTime.UtcNow, "");
        service.Setup(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), source.Token))
            .Returns(Task.CompletedTask);

        await handler.HandleAsync(message, source.Token);

        service.Verify(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), source.Token), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Test]
    public void HandleAsync_PropagatesProcessingException()
    {
        var message = new EmploymentCheckCompletedEvent(Guid.NewGuid(), true, DateTime.UtcNow, "");
        var failure = new InvalidOperationException("Processing failed");
        service.Setup(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), CancellationToken.None))
            .ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(message, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
    }

    [Test]
    public void HandleAsync_PropagatesCancellationException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var message = new EmploymentCheckCompletedEvent(Guid.NewGuid(), true, DateTime.UtcNow, "");
        service.Setup(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), source.Token))
            .Returns(Task.FromCanceled(source.Token));

        Assert.ThrowsAsync<TaskCanceledException>(() =>
            handler.HandleAsync(message, source.Token));
    }
}
