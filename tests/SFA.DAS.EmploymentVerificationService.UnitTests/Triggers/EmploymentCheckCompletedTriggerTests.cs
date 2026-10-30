using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Triggers;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Triggers;

[TestFixture]
public class EmploymentCheckCompletedTriggerTests
{
    [TestCase("true", "", false)]
    [TestCase("false", "", false)]
    [TestCase("null", "CheckFailed", false)]
    [TestCase("true", "", true)]
    [TestCase("false", "", true)]
    [TestCase("null", "CheckFailed", true)]
    public async Task Run_MapsEventDeserializedByConfiguredWorkerSerializer(
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

        // Use the serializer resolved by the same worker configuration as Program.cs,
        // rather than assuming getter-only properties support POCO input binding.
        using var host = new HostBuilder().ConfigureFunctionsWorkerDefaults().Build();
        var serializer = host.Services.GetRequiredService<IOptions<WorkerOptions>>().Value.Serializer!;
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var message = (EmploymentCheckCompletedEvent)(await serializer.DeserializeAsync(
            body, typeof(EmploymentCheckCompletedEvent), CancellationToken.None))!;

        bool? employmentResult = resultJson == "null" ? null : bool.Parse(resultJson);
        var expected = new EmploymentCheckResult(correlationId, employmentResult, checkDate, errorType);
        var service = new Mock<IEmploymentCheckCompletionService>(MockBehavior.Strict);
        service.Setup(x => x.ProcessAsync(expected, CancellationToken.None)).Returns(Task.CompletedTask);

        await new EmploymentCheckCompletedTrigger(service.Object).Run(message, CancellationToken.None);

        Assert.That(message.CheckDate.Kind, Is.EqualTo(DateTimeKind.Utc));
        service.Verify(x => x.ProcessAsync(expected, CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Run_ForwardsCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var message = new EmploymentCheckCompletedEvent(Guid.NewGuid(), false, DateTime.UtcNow, "");
        var service = new Mock<IEmploymentCheckCompletionService>(MockBehavior.Strict);
        service.Setup(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), source.Token))
            .Returns(Task.CompletedTask);

        await new EmploymentCheckCompletedTrigger(service.Object).Run(message, source.Token);

        service.Verify(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), source.Token), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Test]
    public void Run_PropagatesProcessingException()
    {
        var message = new EmploymentCheckCompletedEvent(Guid.NewGuid(), true, DateTime.UtcNow, "");
        var failure = new InvalidOperationException("Processing failed");
        var service = new Mock<IEmploymentCheckCompletionService>(MockBehavior.Strict);
        service.Setup(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), CancellationToken.None))
            .ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EmploymentCheckCompletedTrigger(service.Object).Run(message, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
    }

    [Test]
    public void Run_PropagatesCancellationException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var message = new EmploymentCheckCompletedEvent(Guid.NewGuid(), true, DateTime.UtcNow, "");
        var service = new Mock<IEmploymentCheckCompletionService>(MockBehavior.Strict);
        service.Setup(x => x.ProcessAsync(It.IsAny<EmploymentCheckResult>(), source.Token))
            .Returns(Task.FromCanceled(source.Token));

        Assert.ThrowsAsync<TaskCanceledException>(() =>
            new EmploymentCheckCompletedTrigger(service.Object).Run(message, source.Token));
    }
}
