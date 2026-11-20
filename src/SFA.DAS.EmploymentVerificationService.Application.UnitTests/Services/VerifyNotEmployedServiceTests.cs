using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Application.Persistence;
using SFA.DAS.EmploymentVerificationService.Application.Services;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;
using SFA.DAS.EmploymentVerificationService.Models;
using CheckStatus = SFA.DAS.EmploymentVerificationService.Application.Models.EmploymentCheckStatus;

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

    [TestCase(0, 30)]
    [TestCase(15, 15)]
    public async Task ProcessAsync_SavesVerificationAndCheckBeforeSending(int duration, int expectedDuration)
    {
        // Arrange
        command.DurationInDays = duration;
        LearnerEmploymentVerification? saved = null;
        EmploymentCheckRequest? sent = null;
        var saveCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Setup(x => x.VerificationExists(command.CorrelationId, CancellationToken.None)).ReturnsAsync(false);
        repository.Setup(x => x.SaveVerificationAsync(It.IsAny<LearnerEmploymentVerification>(), CancellationToken.None))
            .Callback<LearnerEmploymentVerification, CancellationToken>((verification, _) => saved = verification)
            .Returns(saveCompleted.Task);
        client.Setup(x => x.SendAsync(It.IsAny<EmploymentCheckRequest>(), CancellationToken.None))
            .Callback<EmploymentCheckRequest, CancellationToken>((request, _) => sent = request)
            .Returns(Task.CompletedTask);

        // Act
        var processing = service.ProcessAsync(command, CancellationToken.None);

        // Assert
        client.Verify(x => x.SendAsync(It.IsAny<EmploymentCheckRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        saveCompleted.SetResult();
        await processing;

        repository.Verify(x => x.SaveVerificationAsync(It.IsAny<LearnerEmploymentVerification>(), CancellationToken.None), Times.Once);
        client.Verify(x => x.SendAsync(It.IsAny<EmploymentCheckRequest>(), CancellationToken.None), Times.Once);
        var verification = saved!;
        var link = verification.LearnerEmploymentChecks.Single();
        var check = link.Detail;
        Assert.Multiple(() =>
        {
            Assert.That(verification.Detail.ExternalCorrelationId, Is.EqualTo(command.CorrelationId));
            Assert.That(verification.Detail.Type, Is.EqualTo(EmploymentVerificationType.NonEmployment));
            Assert.That(verification.Detail.Status, Is.EqualTo(EmploymentVerificationStatus.InProgress));
            Assert.That(verification.Detail.StartDate, Is.EqualTo(DateOnly.FromDateTime(command.StartDate)));
            Assert.That(verification.Detail.DurationInDays, Is.EqualTo(expectedDuration));
            Assert.That(verification.Detail.Reference, Is.EqualTo(command.Reference));
            Assert.That(verification.UniqueLearnerNumber, Is.EqualTo(command.ULN));
            Assert.That(verification.EmployerAccountId, Is.EqualTo(command.EmployerAccountIdentifier));
            Assert.That(verification.ApprenticeshipId, Is.EqualTo(command.ApprenticeshipId));
            Assert.That(link.UniqueLearnerNumber, Is.EqualTo(command.ULN));
            Assert.That(link.EmployerAccountId, Is.EqualTo(command.EmployerAccountIdentifier));
            Assert.That(check.CheckCorrelationId, Is.Not.EqualTo(command.CorrelationId).And.Not.EqualTo(Guid.Empty));
            Assert.That(sent!.CorrelationId, Is.EqualTo(check.CheckCorrelationId));
            Assert.That(sent.EndDate, Is.EqualTo(command.StartDate.AddDays(expectedDuration)));
            Assert.That(check.StartDate, Is.EqualTo(verification.Detail.StartDate));
            Assert.That(check.EndDate, Is.EqualTo(DateOnly.FromDateTime(sent.EndDate)));
            Assert.That(check.Reference, Is.EqualTo(command.Reference));
            Assert.That(check.RequestTime, Is.EqualTo(sent.CreatedOn));
            Assert.That(check.Status, Is.EqualTo((byte)CheckStatus.InProgress));
        });
    }

    [Test]
    public async Task ProcessAsync_IgnoresExistingRequest()
    {
        repository.Setup(x => x.VerificationExists(command.CorrelationId, CancellationToken.None)).ReturnsAsync(true);

        await service.ProcessAsync(command, CancellationToken.None);

        repository.Verify(x => x.VerificationExists(command.CorrelationId, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
        client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ProcessAsync_PropagatesCancellationTokenToAllOperations()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var token = source.Token;
        repository.Setup(x => x.VerificationExists(command.CorrelationId, token)).ReturnsAsync(false);
        repository.Setup(x => x.SaveVerificationAsync(It.IsAny<LearnerEmploymentVerification>(), token)).Returns(Task.CompletedTask);
        client.Setup(x => x.SendAsync(It.IsAny<EmploymentCheckRequest>(), token)).Returns(Task.CompletedTask);

        // Act
        await service.ProcessAsync(command, token);

        // Assert
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
        // Arrange
        var failure = new InvalidOperationException("Save failed");
        repository.Setup(x => x.VerificationExists(command.CorrelationId, CancellationToken.None)).ReturnsAsync(false);
        repository.Setup(x => x.SaveVerificationAsync(It.IsAny<LearnerEmploymentVerification>(), CancellationToken.None)).ThrowsAsync(failure);

        // Act and Assert
        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAsync(command, CancellationToken.None));

        Assert.That(thrown, Is.SameAs(failure));
        client.VerifyNoOtherCalls();
    }

}
