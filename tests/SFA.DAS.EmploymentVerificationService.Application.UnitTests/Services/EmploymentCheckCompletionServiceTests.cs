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
    private Mock<IEmploymentCheckStore> store = null!;
    private Mock<IEmploymentVerificationPublisher> publisher = null!;
    private EmploymentCheckCompletionService service = null!;
    private EmploymentCheckRequest request = null!;
    private EmploymentCheckResult result = null!;

    [SetUp]
    public void SetUp()
    {
        store = new Mock<IEmploymentCheckStore>(MockBehavior.Strict);
        publisher = new Mock<IEmploymentVerificationPublisher>(MockBehavior.Strict);
        service = new EmploymentCheckCompletionService(store.Object, publisher.Object);
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
        request = request with { ApprenticeshipId = apprenticeshipId };
        result = result with { EmploymentResult = employed };
        var expected = new EmploymentCheckCompletion(request.CorrelationId, employed, 2, result.CheckDate);
        store.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);
        store.Setup(x => x.RecordCompletionAsync(expected, CancellationToken.None)).Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.Is<VerifyNotEmployedCompleted>(message =>
            message.CorrelationId == request.CorrelationId &&
            message.ULN == request.ULN &&
            message.ApprenticeshipId == request.ApprenticeshipId &&
            message.EmployerAccountIdentifier == request.AccountId &&
            message.NotEmployed == notEmployed), CancellationToken.None)).Returns(Task.CompletedTask);

        await service.ProcessAsync(result, CancellationToken.None);

        store.VerifyAll();
        publisher.VerifyAll();
    }

    [Test]
    public async Task ProcessAsync_IgnoresUnmatchedCorrelationId()
    {
        store.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None))
            .ReturnsAsync((EmploymentCheckRequest?)null);

        await service.ProcessAsync(result, CancellationToken.None);

        store.Verify(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None), Times.Once);
        store.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_AwaitsPersistenceBeforePublishing()
    {
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);
        store.Setup(x => x.RecordCompletionAsync(It.IsAny<EmploymentCheckCompletion>(), CancellationToken.None)).Returns(saved.Task);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None)).Returns(Task.CompletedTask);

        var processing = service.ProcessAsync(result, CancellationToken.None);

        publisher.VerifyNoOtherCalls();
        saved.SetResult();
        await processing;

        publisher.Verify(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), CancellationToken.None), Times.Once);
    }

    [Test]
    public void ProcessAsync_PersistenceFailurePreventsPublishing()
    {
        var failure = new InvalidOperationException("Save failed");
        store.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);
        store.Setup(x => x.RecordCompletionAsync(It.IsAny<EmploymentCheckCompletion>(), CancellationToken.None)).ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(result, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
        publisher.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_PropagatesCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var token = source.Token;
        store.Setup(x => x.GetRequestAsync(result.CorrelationId, token)).ReturnsAsync(request);
        store.Setup(x => x.RecordCompletionAsync(It.IsAny<EmploymentCheckCompletion>(), token)).Returns(Task.CompletedTask);
        publisher.Setup(x => x.PublishAsync(It.IsAny<VerifyNotEmployedCompleted>(), token)).Returns(Task.CompletedTask);

        await service.ProcessAsync(result, token);

        store.VerifyAll();
        publisher.VerifyAll();
    }

    [Test]
    public void ProcessAsync_DoesNotStartWhenAlreadyCancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => service.ProcessAsync(result, source.Token));

        store.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }

    [TestCase(null, null)]
    [TestCase(true, "error")]
    [TestCase(false, "error")]
    [TestCase(true, " ")]
    public void ProcessAsync_RejectsUnsupportedFailureWithoutSavingOrPublishing(bool? employed, string? errorType)
    {
        result = result with { EmploymentResult = employed, ErrorType = errorType };
        store.Setup(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None)).ReturnsAsync(request);

        Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(result, CancellationToken.None));

        store.Verify(x => x.GetRequestAsync(result.CorrelationId, CancellationToken.None), Times.Once);
        store.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }
}
