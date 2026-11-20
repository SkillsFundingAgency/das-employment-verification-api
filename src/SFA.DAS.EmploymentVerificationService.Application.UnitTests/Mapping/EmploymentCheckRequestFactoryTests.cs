using Moq;
using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Application.UnitTests.Mapping;

[TestFixture]
public class EmploymentCheckRequestFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 34, 56, TimeSpan.Zero);
    private EmploymentCheckRequestFactory factory = null!;

    [SetUp]
    public void SetUp()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(Now);
        factory = new EmploymentCheckRequestFactory(clock.Object);
    }

    [TestCase(15)]
    [TestCase(30)]
    public void Create_UsesEffectiveDuration(int duration)
    {
        // Arrange
        var command = new VerifyNotEmployed
        {
            StartDate = new DateTime(2026, 1, 20, 9, 30, 0, DateTimeKind.Utc),
            DurationInDays = duration
        };

        // Act
        var request = factory.Create(command, Guid.NewGuid(), duration);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(request.StartDate, Is.EqualTo(command.StartDate));
            Assert.That(request.EndDate, Is.EqualTo(command.StartDate.AddDays(duration)));
        });
    }

    [Test]
    public void Create_SetsInitialStatusAndAttemptCount()
    {
        // Act
        var request = factory.Create(new VerifyNotEmployed(), Guid.NewGuid(), 30);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(request.Status, Is.EqualTo((byte)EmploymentCheckStatus.InProgress));
            Assert.That(request.NumberOfAttempts, Is.EqualTo(1));
        });
    }

    [TestCase(12345L, "reference")]
    [TestCase(0L, null)]
    public void Create_MapsIdentifiersAndReference(long apprenticeshipId, string? reference)
    {
        // Arrange
        var command = new VerifyNotEmployed
        {
            CorrelationId = Guid.NewGuid(),
            ULN = 1234567890L,
            EmployerAccountIdentifier = 987654321L,
            ApprenticeshipId = apprenticeshipId,
            Reference = reference
        };

        // Act
        var checkCorrelationId = Guid.NewGuid();
        var request = factory.Create(command, checkCorrelationId, 30);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(request.CorrelationId, Is.EqualTo(checkCorrelationId));
            Assert.That(request.ULN, Is.EqualTo(command.ULN));
            Assert.That(request.EmployerAccountIdentifier, Is.EqualTo(command.EmployerAccountIdentifier));
            Assert.That(request.ApprenticeshipId, Is.EqualTo(apprenticeshipId));
            Assert.That(request.Reference, Is.EqualTo(reference));
        });
    }

    [Test]
    public void Create_UsesCurrentUtcTimeFromTimeProvider()
    {
        // Act
        var request = factory.Create(new VerifyNotEmployed(), Guid.NewGuid(), 30);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(request.CreatedOn, Is.EqualTo(Now.UtcDateTime));
            Assert.That(request.CreatedOn.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

}
