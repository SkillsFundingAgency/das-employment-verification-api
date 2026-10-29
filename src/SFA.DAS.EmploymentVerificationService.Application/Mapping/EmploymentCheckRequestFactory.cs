using SFA.DAS.EmploymentVerificationService.Application.Models;
using SFA.DAS.EmploymentVerificationService.Messages.Learner.Commands;

namespace SFA.DAS.EmploymentVerificationService.Application.Mapping;

public sealed class EmploymentCheckRequestFactory(TimeProvider timeProvider)
{
    public EmploymentCheckRequest Create(VerifyNotEmployed command)
    {
        return new EmploymentCheckRequest(
            command.CorrelationId,
            command.ULN,
            command.EmployerAccountIdentifier,
            command.ApprenticeshipId,
            command.Reference,
            command.StartDate,
            command.StartDate.AddDays(command.DurationInDays ?? 60),
            1,
            (byte)EmploymentCheckStatus.InProgress,
            timeProvider.GetUtcNow().UtcDateTime);
    }
}
