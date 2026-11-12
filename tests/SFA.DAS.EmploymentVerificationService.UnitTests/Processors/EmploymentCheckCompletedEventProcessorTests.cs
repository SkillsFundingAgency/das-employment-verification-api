using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentCheck.Types;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Processors;

[TestFixture]
public class EmploymentCheckCompletedEventProcessorTests
{
    private Mock<IEmploymentCheckCompletionService> service = null!;
    private EmploymentCheckCompletedEventProcessor processor = null!;

    [SetUp]
    public void SetUp()
    {
        service = new Mock<IEmploymentCheckCompletionService>(MockBehavior.Strict);
        processor = new EmploymentCheckCompletedEventProcessor(service.Object);
    }

    [TestCase(true, "")]
    [TestCase(false, "")]
    [TestCase(null, "CheckFailed")]
    public async Task ProcessAsync_MapsCompletionAndForwardsCancellationToken(bool? employed, string errorType)
    {
        var correlationId = Guid.NewGuid();
        var checkDate = new DateTime(2026, 10, 30, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234567);
        var message = new EmploymentCheckCompletedEvent(correlationId, employed, checkDate, errorType);
        var expected = new EmploymentCheckResult(correlationId, employed, checkDate, errorType);
        using var cancellation = new CancellationTokenSource();
        service.Setup(x => x.ProcessAsync(expected, cancellation.Token)).Returns(Task.CompletedTask);

        await processor.ProcessAsync(BinaryData.FromObjectAsJson(message), cancellation.Token);

        service.Verify(x => x.ProcessAsync(expected, cancellation.Token), Times.Once);
        service.Verify(x => x.ProcessAsync(It.Is<EmploymentCheckResult>(result =>
            result.CheckDate.Kind == DateTimeKind.Utc), cancellation.Token), Times.Once);
        service.VerifyNoOtherCalls();
    }
}
