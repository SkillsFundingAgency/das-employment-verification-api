using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Functions.Handlers;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.UnitTests.Handlers;

[TestFixture]
public class VerifyNotEmployedHandlerTests
{
    private Mock<IVerifyNotEmployedService> service = null!;
    private VerifyNotEmployedHandler handler = null!;

    [SetUp]
    public void SetUp()
    {
        service = new Mock<IVerifyNotEmployedService>(MockBehavior.Strict);
        handler = new VerifyNotEmployedHandler(service.Object);
    }

    [TestCase(30, 789L, "reference")]
    [TestCase(null, null, null)]
    public async Task HandleAsync_ForwardsDeserializedCommandValues(int? duration, long? apprenticeshipId, string? reference)
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
        service.Setup(x => x.ProcessAsync(It.Is<VerifyNotEmployed>(received =>
            received.CorrelationId == command.CorrelationId && received.ULN == command.ULN &&
            received.EmployerAccountIdentifier == command.EmployerAccountIdentifier &&
            received.ApprenticeshipId == command.ApprenticeshipId && received.StartDate == command.StartDate &&
            received.DurationInDays == command.DurationInDays && received.Reference == command.Reference), CancellationToken.None))
            .Returns(Task.CompletedTask);

        await handler.HandleAsync(BinaryData.FromObjectAsJson(command), CancellationToken.None);

        service.Verify(x => x.ProcessAsync(It.Is<VerifyNotEmployed>(received =>
            received.CorrelationId == command.CorrelationId && received.ULN == command.ULN &&
            received.EmployerAccountIdentifier == command.EmployerAccountIdentifier &&
            received.ApprenticeshipId == command.ApprenticeshipId && received.StartDate == command.StartDate &&
            received.DurationInDays == command.DurationInDays && received.Reference == command.Reference), CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Test]
    public async Task HandleAsync_PropagatesCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var command = new VerifyNotEmployed();
        service.Setup(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), source.Token)).Returns(Task.CompletedTask);

        await handler.HandleAsync(BinaryData.FromObjectAsJson(command), source.Token);

        service.Verify(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), source.Token), Times.Once);
    }

    [Test]
    public void HandleAsync_PropagatesProcessingException()
    {
        var command = new VerifyNotEmployed();
        var failure = new InvalidOperationException("Processing failed");
        service.Setup(x => x.ProcessAsync(It.IsAny<VerifyNotEmployed>(), CancellationToken.None)).ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(BinaryData.FromObjectAsJson(command), CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
    }
}
