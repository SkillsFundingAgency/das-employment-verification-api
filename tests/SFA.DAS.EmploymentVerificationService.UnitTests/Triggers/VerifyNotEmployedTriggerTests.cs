using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Triggers;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Triggers;

[TestFixture]
public class VerifyNotEmployedTriggerTests
{
    [TestCase(30, 789L, "reference")]
    [TestCase(null, null, null)]
    public async Task Run_PassesOriginalCommandAndValuesToService(int? duration, long? apprenticeshipId, string? reference)
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
        var service = new Mock<IVerifyNotEmployedService>(MockBehavior.Strict);
        service.Setup(x => x.ProcessAsync(It.Is<VerifyNotEmployed>(received =>
            ReferenceEquals(received, command) &&
            received.CorrelationId == command.CorrelationId &&
            received.ULN == 1234567890L &&
            received.EmployerAccountIdentifier == 456 &&
            received.ApprenticeshipId == apprenticeshipId &&
            received.StartDate == new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) &&
            received.DurationInDays == duration && received.Reference == reference), CancellationToken.None))
            .Returns(Task.CompletedTask);

        await new VerifyNotEmployedTrigger(service.Object, NullLogger<VerifyNotEmployedTrigger>.Instance).Run(command, CancellationToken.None);

        service.Verify(x => x.ProcessAsync(command, CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Run_PropagatesCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var command = new VerifyNotEmployed();
        var service = new Mock<IVerifyNotEmployedService>(MockBehavior.Strict);
        service.Setup(x => x.ProcessAsync(command, source.Token)).Returns(Task.CompletedTask);

        await new VerifyNotEmployedTrigger(service.Object, NullLogger<VerifyNotEmployedTrigger>.Instance).Run(command, source.Token);

        service.Verify(x => x.ProcessAsync(command, source.Token), Times.Once);
    }

    [Test]
    public void Run_PropagatesProcessingException()
    {
        var command = new VerifyNotEmployed();
        var failure = new InvalidOperationException("Processing failed");
        var service = new Mock<IVerifyNotEmployedService>(MockBehavior.Strict);
        service.Setup(x => x.ProcessAsync(command, CancellationToken.None)).ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() =>
            new VerifyNotEmployedTrigger(service.Object, NullLogger<VerifyNotEmployedTrigger>.Instance).Run(command, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
    }
}
