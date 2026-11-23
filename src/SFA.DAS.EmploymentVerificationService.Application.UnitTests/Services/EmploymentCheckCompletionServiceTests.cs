using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;
using SFA.DAS.EmploymentVerificationService.Models;
using EmploymentCheckStatus = SFA.DAS.EmploymentVerificationService.Models.EmploymentCheckStatus;

namespace SFA.DAS.EmploymentVerificationService.Application.UnitTests.Services;

[TestFixture]
public class EmploymentCheckCompletionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 34, 56, TimeSpan.Zero);
    private Mock<IEmploymentCheckRepository> repository = null!;
    private Mock<IMessagePublisher> publisher = null!;
    private EmploymentCheckCompletionService service = null!;
    private LearnerEmploymentVerification verification = null!;
    private EmploymentCheck check = null!;
    private EmploymentCheckResult result = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new Mock<IEmploymentCheckRepository>(MockBehavior.Strict);
        publisher = new Mock<IMessagePublisher>(MockBehavior.Strict);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(Now);
        service = new EmploymentCheckCompletionService(repository.Object, publisher.Object, clock.Object, NullLogger<EmploymentCheckCompletionService>.Instance);
        check = new EmploymentCheck
        {
            CheckCorrelationId = Guid.NewGuid(),
            Status = (byte)EmploymentCheckStatus.InProgress
        };
        verification = new LearnerEmploymentVerification
        {
            Detail = new EmploymentVerification
            {
                ExternalCorrelationId = Guid.NewGuid(),
                Status = EmploymentVerificationStatus.InProgress
            },
            UniqueLearnerNumber = 1234567890,
            EmployerAccountId = 456,
            ApprenticeshipId = 789,
            LearnerEmploymentChecks = [new LearnerEmploymentCheck { Detail = check }]
        };
        result = new EmploymentCheckResult(check.CheckCorrelationId, false,
            new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc), null);
    }

    [TestCase(false, true, EmploymentVerificationStatus.NotEmployed)]
    [TestCase(true, false, EmploymentVerificationStatus.Employed)]
    public async Task ProcessAsync_RecordsResultAndPublishesMappedMessage(
        bool employed, bool notEmployed, EmploymentVerificationStatus expectedStatus)
    {
        // Arrange
        result = result with { EmploymentResult = employed };
        repository.Setup(x => x.GetVerificationForCheckAsync(check.CheckCorrelationId, CancellationToken.None)).ReturnsAsync(verification);
        repository.Setup(x => x.SaveChangesAsync(CancellationToken.None))
            .Callback(() =>
            {
                Assert.That(check.Status, Is.EqualTo((byte)EmploymentCheckStatus.Completed));
                Assert.That(verification.Detail.Status, Is.EqualTo(expectedStatus));
            })
            .Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.Is<VerifyNotEmployedCompleted>(message =>
            message.CorrelationId == verification.Detail.ExternalCorrelationId &&
            message.CorrelationId != result.CorrelationId &&
            message.ULN == verification.UniqueLearnerNumber &&
            message.ApprenticeshipId == verification.ApprenticeshipId &&
            message.EmployerAccountIdentifier == verification.EmployerAccountId &&
            message.NotEmployed == notEmployed), CancellationToken.None, true)).Returns(Task.CompletedTask);

        // Act
        await service.ProcessAsync(result, CancellationToken.None);

        // Assert
        repository.VerifyAll();
        repository.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
        publisher.VerifyAll();
    }

    [Test]
    public async Task ProcessAsync_RecordsCurrentUtcTimeInsteadOfIncomingCheckDate()
    {
        // Arrange
        repository.Setup(x => x.GetVerificationForCheckAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(verification);
        repository.Setup(x => x.SaveChangesAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None, true)).Returns(Task.CompletedTask);

        // Act
        await service.ProcessAsync(result, CancellationToken.None);

        // Assert
        Assert.That(check.ResponseTime, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(check.ResponseTime, Is.EqualTo(Now.UtcDateTime));
            Assert.That(check.ResponseTime!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(check.ResponseTime, Is.Not.EqualTo(result.CheckDate));
        });
    }

    [Test]
    public async Task ProcessAsync_IgnoresUnmatchedCorrelationId()
    {
        // Arrange
        repository.Setup(x => x.GetVerificationForCheckAsync(result.CorrelationId, CancellationToken.None))
            .ReturnsAsync((LearnerEmploymentVerification?)null);

        // Act
        await service.ProcessAsync(result, CancellationToken.None);

        // Assert
        repository.Verify(x => x.GetVerificationForCheckAsync(result.CorrelationId, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_AwaitsPersistenceBeforePublishing()
    {
        // Arrange
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Setup(x => x.GetVerificationForCheckAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(verification);
        repository.Setup(x => x.SaveChangesAsync(CancellationToken.None)).Returns(saved.Task);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None, true)).Returns(Task.CompletedTask);

        // Act
        var processing = service.ProcessAsync(result, CancellationToken.None);

        // Assert
        publisher.VerifyNoOtherCalls();
        saved.SetResult();
        await processing;

        publisher.Verify(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None, true), Times.Once);
    }

    [Test]
    public void ProcessAsync_PersistenceFailurePreventsPublishing()
    {
        // Arrange
        var failure = new InvalidOperationException("Save failed");
        repository.Setup(x => x.GetVerificationForCheckAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(verification);
        repository.Setup(x => x.SaveChangesAsync(CancellationToken.None)).ThrowsAsync(failure);

        // Act and Assert
        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(result, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
        publisher.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_PropagatesCancellationToken()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var token = source.Token;
        repository.Setup(x => x.GetVerificationForCheckAsync(result.CorrelationId, token)).ReturnsAsync(verification);
        repository.Setup(x => x.SaveChangesAsync(token)).Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), token, true)).Returns(Task.CompletedTask);

        // Act
        await service.ProcessAsync(result, token);

        // Assert
        repository.VerifyAll();
        publisher.VerifyAll();
    }

    [Test]
    public void ProcessAsync_DoesNotStartWhenAlreadyCancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => service.ProcessAsync(result, source.Token));

        repository.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }

    [TestCase(null, null)]
    [TestCase(true, "error")]
    [TestCase(false, "error")]
    [TestCase(true, " ")]
    public void ProcessAsync_RejectsUnsupportedFailureWithoutSavingOrPublishing(bool? employed, string? errorType)
    {
        // Arrange
        result = result with { EmploymentResult = employed, ErrorType = errorType };
        repository.Setup(x => x.GetVerificationForCheckAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(verification);

        // Act and Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(result, CancellationToken.None));

        repository.Verify(x => x.GetVerificationForCheckAsync(result.CorrelationId, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }
}
