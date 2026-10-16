namespace SFA.DAS.EmploymentVerificationService.Messages.Learner.Events;

public class VerifyNotEmployedCompleted
{
    public Guid CorrelationId { get; set; }
    public long ULN { get; set; }
    public long EmployerAccountIdentifier { get; set; }
    public bool NotEmployed { get; set; }
}
