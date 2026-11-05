using Microsoft.Extensions.Logging.Abstractions;
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
    private Mock<IEmploymentCheckRepository> repository = null!;
    private Mock<IEmploymentCheckClient> client = null!;
    private EmploymentCheckRequestFactory factory = null!;
    private VerifyNotEmployedService service = null!;
    private VerifyNotEmployed command = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new Mock<IEmploymentCheckRepository>(MockBehavior.Strict);
        client = new Mock<IEmploymentCheckClient>(MockBehavior.Strict);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        factory = new EmploymentCheckRequestFactory(clock.Object);
        service = new VerifyNotEmployedService(repository.Object, factory, client.Object, NullLogger<VerifyNotEmployedService>.Instance);
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
        repository.Setup(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None)).ReturnsAsync(false);
        repository.Setup(x => x.SaveRequestAsync(expected, CancellationToken.None))
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

        repository.Verify(x => x.SaveRequestAsync(expected, CancellationToken.None), Times.Once);
        client.Verify(x => x.SendAsync(expected, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_IgnoresExistingRequest()
    {
        repository.Setup(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None)).ReturnsAsync(true);

        await service.ProcessAsync(command, CancellationToken.None);

        repository.Verify(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
        client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_PropagatesCancellationTokenToAllOperations()
    {
        using var source = new CancellationTokenSource();
        var token = source.Token;
        var expected = factory.Create(command);
        repository.Setup(x => x.ExistsAsync(command.CorrelationId, token)).ReturnsAsync(false);
        repository.Setup(x => x.SaveRequestAsync(expected, token)).Returns(Task.CompletedTask);
        client.Setup(x => x.SendAsync(expected, token)).Returns(Task.CompletedTask);

        await service.ProcessAsync(command, token);

        repository.VerifyAll();
        client.VerifyAll();
    }

    [Test]
    public void ProcessAsync_DoesNotStartWhenAlreadyCancelled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => service.ProcessAsync(command, source.Token));

        repository.VerifyNoOtherCalls();
        client.VerifyNoOtherCalls();
    }

    [Test]
    public void ProcessAsync_SaveFailurePreventsSend()
    {
        var failure = new InvalidOperationException("Save failed");
        repository.Setup(x => x.ExistsAsync(command.CorrelationId, CancellationToken.None)).ReturnsAsync(false);
        repository.Setup(x => x.SaveRequestAsync(factory.Create(command), CancellationToken.None)).ThrowsAsync(failure);

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(command, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
        client.VerifyNoOtherCalls();
    }

}
