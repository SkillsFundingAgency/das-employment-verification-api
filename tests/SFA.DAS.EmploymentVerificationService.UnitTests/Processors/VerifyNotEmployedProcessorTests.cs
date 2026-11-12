using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Processors;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Processors;

[TestFixture]
public class VerifyNotEmployedProcessorTests
{
    private Mock<IVerifyNotEmployedService> service = null!;
    private VerifyNotEmployedProcessor processor = null!;

    [SetUp]
    public void SetUp()
    {
        service = new Mock<IVerifyNotEmployedService>(MockBehavior.Strict);
        processor = new VerifyNotEmployedProcessor(service.Object);
    }

    [TestCase(30, 789L, "reference")]
    [TestCase(null, null, null)]
    public async Task ProcessAsync_ForwardsCommandAndCancellationToken(int? duration, long? apprenticeshipId, string? reference)
    {
        var command = new VerifyNotEmployed
        {
            CorrelationId = Guid.NewGuid(),
            ULN = 1234567890L,
            EmployerAccountIdentifier = 456,
            ApprenticeshipId = apprenticeshipId,
            StartDate = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            DurationInDays = duration,
            Reference = reference
        };
        using var cancellation = new CancellationTokenSource();
        VerifyNotEmployed? received = null;
        service.Setup(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), cancellation.Token))
            .Callback<VerifyNotEmployed, CancellationToken>((message, _) => received = message)
            .Returns(Task.CompletedTask);

        await processor.ProcessAsync(BinaryData.FromObjectAsJson(command), cancellation.Token);

        Assert.That(received, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(received!.CorrelationId, Is.EqualTo(command.CorrelationId));
            Assert.That(received.ULN, Is.EqualTo(command.ULN));
            Assert.That(received.EmployerAccountIdentifier, Is.EqualTo(command.EmployerAccountIdentifier));
            Assert.That(received.ApprenticeshipId, Is.EqualTo(apprenticeshipId));
            Assert.That(received.StartDate, Is.EqualTo(command.StartDate));
            Assert.That(received.DurationInDays, Is.EqualTo(duration));
            Assert.That(received.Reference, Is.EqualTo(reference));
        });
        service.Verify(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), cancellation.Token), Times.Once);
        service.VerifyNoOtherCalls();
    }

}
