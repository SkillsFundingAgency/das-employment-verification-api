namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

public abstract class EmploymentVerificationEventBase
{
    public Guid CorrelationId { get; set; }
    public long ULN { get; set; }
    public long? ApprenticeshipId { get; set; }
    public long EmployerAccountIdentifier { get; set; }
}
