using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Application.UnitTests.Services;

[TestFixture]
public class VerifyNotEmployedServiceTests
{
    private Mock<IEmploymentCheckStore> store = null!;
    private Mock<IEmploymentCheckClient> client = null!;
    private EmploymentCheckRequestFactory factory = null!;
    private VerifyNotEmployedService service = null!;
    private VerifyNotEmployed command = null!;

    [SetUp]
    public void SetUp()
    {
        store = new Mock<IEmploymentCheckStore>(MockBehavior.Strict);
        client = new Mock<IEmploymentCheckClient>(MockBehavior.Strict);
        factory = new EmploymentCheckRequestFactory(new FixedTimeProvider());
        service = new VerifyNotEmployedService(store.Object, factory, client.Object);
        command = new VerifyNotEmployed
        {
            CorrelationId = Guid.NewGuid(),
            ULN = 1234567890,
            EmployerAccountIdentifier = 456,
            ApprenticeshipId = 789,
            Reference = "reference",
            StartDate = new DateTime(2026, 1, 1),
            DurationInDays = 30
        };
    }

    [Test]
    public async Task ProcessAsync_SavesNewRequestBeforeSendingSameRequest()
    {
        var expected = factory.Create(command);
        EmploymentCheckRequest? saved = null;
        var saveCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Setup(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None)).ReturnsAsync(false);
        store.Setup(x => x.SaveRequestAsync(expected, CancellationToken.None))
            .Callback<EmploymentCheckRequest, CancellationToken>((request, _) => saved = request)
            .Returns(saveCompleted.Task);
        client.Setup(x => x.SendAsync(expected, CancellationToken.None))
            .Callback<EmploymentCheckRequest, CancellationToken>((request, _) =>
                Assert.That(request, Is.SameAs(saved)))
            .Returns(Task.CompletedTask);

        var processing = service.ProcessAsync(command, CancellationToken.None);

        client.Verify(x => x.SendAsync(It.IsAny<EmploymentCheckRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        saveCompleted.SetResult();
        await processing;

        store.Verify(x => x.SaveRequestAsync(expected, CancellationToken.None), Times.Once);
        client.Verify(x => x.SendAsync(expected, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_IgnoresExistingRequest()
    {
        store.Setup(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None)).ReturnsAsync(true);

        await service.ProcessAsync(command, CancellationToken.None);

        store.Verify(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None), Times.Once);
        store.VerifyNoOtherCalls();
        client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_PropagatesCancellationTokenToAllOperations()
    {
        using var source = new CancellationTokenSource();
        var token = source.Token;
        var expected = factory.Create(command);
        store.Setup(x => x.ExistsAsync(command.CorrelationId, token)).ReturnsAsync(false);
        store.Setup(x => x.SaveRequestAsync(expected, token)).Returns(Task.CompletedTask);
        client.Setup(x => x.SendAsync(expected, token)).Returns(Task.CompletedTask);

        await service.ProcessAsync(command, token);

        store.VerifyAll();
        client.VerifyAll();
    }

    [Test]
    public void ProcessAsync_DoesNotStartWhenAlreadyCancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => service.ProcessAsync(command, source.Token));

        store.VerifyNoOtherCalls();
        client.VerifyNoOtherCalls();
    }

    [Test]
    public void ProcessAsync_SaveFailurePreventsSend()
    {
        var failure = new InvalidOperationException("Save failed");
        store.Setup(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None)).ReturnsAsync(false);
        store.Setup(x => x.SaveRequestAsync(factory.Create(command), CancellationToken.None)).ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(command, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
        client.VerifyNoOtherCalls();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    }
}
