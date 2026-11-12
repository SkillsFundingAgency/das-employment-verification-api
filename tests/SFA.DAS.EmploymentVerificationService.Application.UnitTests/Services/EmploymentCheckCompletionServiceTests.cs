using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

namespace SFA.DAS.EmploymentVerificationService.Application.UnitTests.Services;

[TestFixture]
public class EmploymentCheckCompletionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 34, 56, TimeSpan.Zero);
    private Mock<IEmploymentCheckRepository> repository = null!;
    private Mock<IEmploymentVerificationPublisher> publisher = null!;
    private EmploymentCheckCompletionService service = null!;
    private EmploymentCheckRequest request = null!;
    private EmploymentCheckResult result = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new Mock<IEmploymentCheckRepository>(MockBehavior.Strict);
        publisher = new Mock<IEmploymentVerificationPublisher>(MockBehavior.Strict);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(Now);
        service = new EmploymentCheckCompletionService(repository.Object, publisher.Object, clock.Object, NullLogger<EmploymentCheckCompletionService>.Instance);
        request = new EmploymentCheckRequest(Guid.NewGuid(), 1234567890, 456, 789, "reference",
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 2), 1,
            (byte)EmploymentCheckStatus.InProgress, new DateTime(2026, 1, 1));
        result = new EmploymentCheckResult(request.CorrelationId, false,
            new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc), null);
    }

    [TestCase(false, true, 789L)]
    [TestCase(true, false, null)]
    public async Task ProcessAsync_RecordsResultAndPublishesMappedMessage(bool employed, bool notEmployed, long? apprenticeshipId)
    {
        // Arrange
        request = request with { ApprenticeshipId = apprenticeshipId };
        result = result with { EmploymentResult = employed };
        var expected = new EmploymentCheckCompletion(request.CorrelationId, employed, (byte)EmploymentCheckStatus.Completed, Now.UtcDateTime);
        repository.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);
        repository.Setup(x => x.RecordCompletionAsync(expected, CancellationToken.None)).Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.Is<VerifyNotEmployedCompleted>(message =>
            message.CorrelationId == request.CorrelationId &&
            message.ULN == request.ULN &&
            message.ApprenticeshipId == request.ApprenticeshipId &&
            message.EmployerAccountIdentifier == request.EmployerAccountIdentifier &&
            message.NotEmployed == notEmployed), CancellationToken.None)).Returns(Task.CompletedTask);

        // Act
        await service.ProcessAsync(result, CancellationToken.None);

        // Assert
        repository.VerifyAll();
        publisher.VerifyAll();
    }

    [Test]
    public async Task ProcessAsync_RecordsCurrentUtcTimeInsteadOfIncomingCheckDate()
    {
        // Arrange
        EmploymentCheckCompletion? recorded = null;
        repository.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);
        repository.Setup(x => x.RecordCompletionAsync(It.IsAny<EmploymentCheckCompletion>(), CancellationToken.None))
            .Callback<EmploymentCheckCompletion, CancellationToken>((completion, _) => recorded = completion)
            .Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None)).Returns(Task.CompletedTask);

        // Act
        await service.ProcessAsync(result, CancellationToken.None);

        // Assert
        Assert.That(recorded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(recorded!.CreatedOn, Is.EqualTo(Now.UtcDateTime));
            Assert.That(recorded.CreatedOn.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(recorded.CreatedOn, Is.Not.EqualTo(result.CheckDate));
        });
    }

    [Test]
    public async Task ProcessAsync_IgnoresUnmatchedCorrelationId()
    {
        // Arrange
        repository.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None))
            .ReturnsAsync((EmploymentCheckRequest?)null);

        // Act
        await service.ProcessAsync(result, CancellationToken.None);

        // Assert
        repository.Verify(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_AwaitsPersistenceBeforePublishing()
    {
        // Arrange
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);
        repository.Setup(x => x.RecordCompletionAsync(It.IsAny<EmploymentCheckCompletion>(), CancellationToken.None)).Returns(saved.Task);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None)).Returns(Task.CompletedTask);

        // Act
        var processing = service.ProcessAsync(result, CancellationToken.None);

        // Assert
        publisher.VerifyNoOtherCalls();
        saved.SetResult();
        await processing;

        publisher.Verify(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None), Times.Once);
    }

    [Test]
    public void ProcessAsync_PersistenceFailurePreventsPublishing()
    {
        // Arrange
        var failure = new InvalidOperationException("Save failed");
        repository.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);
        repository.Setup(x => x.RecordCompletionAsync(It.IsAny<EmploymentCheckCompletion>(), CancellationToken.None)).ThrowsAsync(failure);

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
        repository.Setup(x => x.GetRequestAsync(result.CorrelationId, token)).ReturnsAsync(request);
        repository.Setup(x => x.RecordCompletionAsync(It.IsAny<EmploymentCheckCompletion>(), token)).Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), token)).Returns(Task.CompletedTask);

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
        repository.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);

        // Act and Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(result, CancellationToken.None));

        repository.Verify(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }
}
