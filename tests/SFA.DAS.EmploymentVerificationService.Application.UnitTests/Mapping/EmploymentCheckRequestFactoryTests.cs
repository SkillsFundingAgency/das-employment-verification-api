using NUnit.Framework;
using SFA.DAS.EmploymentVerificationService.Application.Mapping;
using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Application.UnitTests.Mapping;

[TestFixture]
public class EmploymentCheckRequestFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 34, 56, TimeSpan.Zero);
    private readonly EmploymentCheckRequestFactory factory = new(new FixedTimeProvider(Now));

    [Test]
    public void Create_UsesExplicitDuration()
    {
        var command = new VerifyNotEmployed
        {
            StartDate = new DateTime(2026, 1, 20, 9, 30, 0, DateTimeKind.Utc),
            DurationInDays = 15
        };

        var request = factory.Create(command);

        Assert.Multiple(() =>
        {
            Assert.That(request.StartDate, Is.EqualTo(command.StartDate));
            Assert.That(request.EndDate, Is.EqualTo(new DateTime(2026, 2, 4, 9, 30, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public void Create_DefaultsMissingDurationToSixtyDays()
    {
        var command = new VerifyNotEmployed { StartDate = new DateTime(2026, 1, 1) };

        var request = factory.Create(command);

        Assert.That(request.EndDate, Is.EqualTo(new DateTime(2026, 3, 2)));
    }

    [Test]
    public void Create_SetsInitialStatusAndAttemptCount()
    {
        var request = factory.Create(new VerifyNotEmployed());

        Assert.Multiple(() =>
        {
            Assert.That(request.Status, Is.EqualTo((byte)EmploymentCheckStatus.InProgress));
            Assert.That(request.NumberOfAttempts, Is.EqualTo(1));
        });
    }

    [TestCase(12345L, "reference")]
    [TestCase(null, null)]
    public void Create_MapsIdentifiersAndReference(long? apprenticeshipId, string? reference)
    {
        var command = new VerifyNotEmployed
        {
            CorrelationId = Guid.NewGuid(),
            ULN = 1234567890L,
            EmployerAccountIdentifier = 987654321L,
            ApprenticeshipId = apprenticeshipId,
            Reference = reference
        };

        var request = factory.Create(command);

        Assert.Multiple(() =>
        {
            Assert.That(request.CorrelationId, Is.EqualTo(command.CorrelationId));
            Assert.That(request.ULN, Is.EqualTo(command.ULN));
            Assert.That(request.AccountId, Is.EqualTo(command.EmployerAccountIdentifier));
            Assert.That(request.ApprenticeshipId, Is.EqualTo(apprenticeshipId));
            Assert.That(request.Reference, Is.EqualTo(reference));
        });
    }

    [Test]
    public void Create_UsesCurrentUtcTimeFromTimeProvider()
    {
        var request = factory.Create(new VerifyNotEmployed());

        Assert.Multiple(() =>
        {
            Assert.That(request.CreatedOn, Is.EqualTo(Now.UtcDateTime));
            Assert.That(request.CreatedOn.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
